using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using CourierService.Domain;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class ReportsRepository : IReportsRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ReportsRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public ReportData GetReport(ReportFilter filter)
        {
            filter = filter ?? new ReportFilter();

            var window = ReportDateWindow.For(filter.FromDate, filter.ToDate, DateTime.UtcNow);
            var fromLocal = window.FromLocal;
            var toLocal = window.ToLocal;
            var fromUtc = window.FromUtc;
            var toUtc = window.ToUtcExclusive;
            const int offsetMinutes = 120;

            var where = new StringBuilder("WHERE p.CreatedAtUtc >= @FromUtc AND p.CreatedAtUtc < @ToUtc");
            if (!string.IsNullOrEmpty(filter.Status)) where.Append(" AND p.Status = @Status");
            if (!string.IsNullOrEmpty(filter.Classification)) where.Append(" AND p.Classification = @Classification");
            if (!string.IsNullOrEmpty(filter.PaymentStatus)) where.Append(" AND p.PaymentStatus = @PaymentStatus");
            var whereSql = where.ToString();

            Action<IDbCommand> addParams = cmd =>
            {
                cmd.AddParameter("@FromUtc", DbType.DateTime2, fromUtc);
                cmd.AddParameter("@ToUtc", DbType.DateTime2, toUtc);
                cmd.AddParameter("@OffsetMinutes", DbType.Int32, offsetMinutes);
                if (!string.IsNullOrEmpty(filter.Status)) cmd.AddParameter("@Status", DbType.String, filter.Status);
                if (!string.IsNullOrEmpty(filter.Classification)) cmd.AddParameter("@Classification", DbType.String, filter.Classification);
                if (!string.IsNullOrEmpty(filter.PaymentStatus)) cmd.AddParameter("@PaymentStatus", DbType.String, filter.PaymentStatus);
            };

            var report = new ReportData();

            using (var connection = _connectionFactory.CreateOpenConnection())
            {
                report.Kpis = ReadKpis(connection, whereSql, addParams);

                var perDay = ReadPoints(connection, addParams,
                    @"SELECT CONVERT(char(10), DATEADD(MINUTE, @OffsetMinutes, p.CreatedAtUtc), 23) AS K, COUNT(*) AS V
                      FROM dbo.Packages p " + whereSql + @"
                      GROUP BY CONVERT(char(10), DATEADD(MINUTE, @OffsetMinutes, p.CreatedAtUtc), 23)
                      ORDER BY K;");
                report.PerDay = FillMissingDays(perDay, fromLocal, toLocal);

                report.ByStatus = ReadPoints(connection, addParams,
                    "SELECT p.Status AS K, COUNT(*) AS V FROM dbo.Packages p " + whereSql + " GROUP BY p.Status ORDER BY p.Status;");
                report.ByClassification = ReadPoints(connection, addParams,
                    "SELECT p.Classification AS K, COUNT(*) AS V FROM dbo.Packages p " + whereSql + " GROUP BY p.Classification ORDER BY p.Classification;");
                report.ByPaymentStatus = ReadPoints(connection, addParams,
                    "SELECT p.PaymentStatus AS K, ISNULL(SUM(p.Fee), 0) AS V FROM dbo.Packages p " + whereSql + " GROUP BY p.PaymentStatus ORDER BY p.PaymentStatus;");

                report.Outstanding = ReadOutstanding(connection, whereSql, addParams);
            }

            return report;
        }

        private static ReportKpis ReadKpis(IDbConnection connection, string whereSql, Action<IDbCommand> addParams)
        {
            var sql = @"
                SELECT
                    COUNT(*) AS Total,
                    ISNULL(SUM(CASE WHEN p.Status = 'ReadyForCollection' THEN 1 ELSE 0 END), 0) AS Ready,
                    ISNULL(SUM(CASE WHEN p.Status = 'Collected' THEN 1 ELSE 0 END), 0) AS Collected,
                    AVG(CASE WHEN p.CollectedAtUtc IS NOT NULL
                             THEN DATEDIFF(HOUR, p.CreatedAtUtc, p.CollectedAtUtc) / 24.0 END) AS AvgDays,
                    ISNULL(SUM(CASE WHEN p.PaymentStatus = 'Paid' THEN p.Fee END), 0) AS FeesPaid,
                    ISNULL(SUM(CASE WHEN p.PaymentStatus = 'Unpaid' THEN p.Fee END), 0) AS FeesUnpaid
                FROM dbo.Packages p " + whereSql + ";";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                addParams(command);

                using (var reader = command.ExecuteReader())
                {
                    reader.Read();
                    var avgOrdinal = reader.GetOrdinal("AvgDays");
                    return new ReportKpis
                    {
                        Total = reader.GetInt32(reader.GetOrdinal("Total")),
                        ReadyForCollection = reader.GetInt32(reader.GetOrdinal("Ready")),
                        Collected = reader.GetInt32(reader.GetOrdinal("Collected")),
                        AverageDaysToCollect = reader.IsDBNull(avgOrdinal)
                            ? (double?)null
                            : Math.Round(Convert.ToDouble(reader.GetValue(avgOrdinal), CultureInfo.InvariantCulture), 1),
                        FeesPaid = reader.GetDecimal(reader.GetOrdinal("FeesPaid")),
                        FeesUnpaid = reader.GetDecimal(reader.GetOrdinal("FeesUnpaid"))
                    };
                }
            }
        }

        private static List<ReportPoint> ReadPoints(IDbConnection connection, Action<IDbCommand> addParams, string sql)
        {
            var points = new List<ReportPoint>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                addParams(command);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        points.Add(new ReportPoint
                        {
                            Label = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                            Value = Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture)
                        });
                    }
                }
            }
            return points;
        }

        private static List<OutstandingPackageRow> ReadOutstanding(IDbConnection connection, string whereSql, Action<IDbCommand> addParams)
        {
            var sql = @"
                SELECT TOP (50)
                    p.F20Identifier, r.FullName, p.Status, sl.Code AS LocationCode,
                    DATEDIFF(DAY, p.CreatedAtUtc, SYSUTCDATETIME()) AS AgeDays
                FROM dbo.Packages p
                INNER JOIN dbo.Recipients r ON r.RecipientId = p.RecipientId
                LEFT JOIN dbo.StorageLocations sl ON sl.StorageLocationId = p.StorageLocationId
                " + whereSql + @" AND p.Status <> 'Collected'
                ORDER BY p.CreatedAtUtc ASC, p.PackageId ASC;";

            var rows = new List<OutstandingPackageRow>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                addParams(command);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var locOrdinal = reader.GetOrdinal("LocationCode");
                        rows.Add(new OutstandingPackageRow
                        {
                            F20Identifier = reader.GetString(reader.GetOrdinal("F20Identifier")),
                            RecipientName = reader.GetString(reader.GetOrdinal("FullName")),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            StorageLocation = reader.IsDBNull(locOrdinal) ? null : reader.GetString(locOrdinal),
                            AgeDays = reader.GetInt32(reader.GetOrdinal("AgeDays"))
                        });
                    }
                }
            }
            return rows;
        }

        // Gives the line chart a point (0) for days with no packages. Skipped for very long ranges.
        private static List<ReportPoint> FillMissingDays(List<ReportPoint> found, DateTime fromLocal, DateTime toLocal)
        {
            if ((toLocal - fromLocal).TotalDays > 366) return found;

            var byDay = new Dictionary<string, decimal>();
            foreach (var p in found) byDay[p.Label] = p.Value;

            var filled = new List<ReportPoint>();
            for (var day = fromLocal; day <= toLocal; day = day.AddDays(1))
            {
                var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                decimal value;
                filled.Add(new ReportPoint { Label = key, Value = byDay.TryGetValue(key, out value) ? value : 0m });
            }
            return filled;
        }
    }
}