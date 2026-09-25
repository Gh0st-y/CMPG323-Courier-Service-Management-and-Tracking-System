using System.Collections.Generic;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DashboardRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public DashboardStats GetStats(string period = "today")
        {
            // "today" = calendar day (UTC). "week" = last 7 days including today. "month" = last 30 days.
            int days;
            switch ((period ?? "today").ToLowerInvariant())
            {
                case "week": days = 7; break;
                case "month": days = 30; break;
                default: days = 1; break;
            }

            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM dbo.Packages
                        WHERE CreatedAtUtc >= DATEADD(DAY, @NegDays, CAST(SYSUTCDATETIME() AS DATE))
                          AND CreatedAtUtc < DATEADD(DAY, 1, CAST(SYSUTCDATETIME() AS DATE))) AS ReceivedInPeriod,
                    (SELECT COUNT(*) FROM dbo.Packages WHERE Status = 'ReadyForCollection') AS ReadyForCollection,
                    (SELECT COUNT(*) FROM dbo.Packages
                        WHERE CollectedAtUtc >= DATEADD(DAY, @NegDays, CAST(SYSUTCDATETIME() AS DATE))
                          AND CollectedAtUtc < DATEADD(DAY, 1, CAST(SYSUTCDATETIME() AS DATE))) AS CollectedInPeriod,
                    (SELECT COUNT(*) FROM dbo.Packages
                        WHERE Status IN ('Registered', 'InStorage', 'ReadyForCollection')) AS Outstanding;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@NegDays", DbType.Int32, -(days - 1));

                using (var reader = command.ExecuteReader())
                {
                    reader.Read();
                    return new DashboardStats
                    {
                        ReceivedToday = reader.GetInt32(reader.GetOrdinal("ReceivedInPeriod")),
                        ReadyForCollection = reader.GetInt32(reader.GetOrdinal("ReadyForCollection")),
                        CollectedToday = reader.GetInt32(reader.GetOrdinal("CollectedInPeriod")),
                        Outstanding = reader.GetInt32(reader.GetOrdinal("Outstanding"))
                    };
                }
            }
        }

        public IEnumerable<RecentActivityItem> GetRecentActivity(int count = 10)
        {
            const string sql = @"
                SELECT TOP (@Count)
                    p.F20Identifier, h.FromStatus, h.ToStatus, u.Username, h.ChangedAtUtc
                FROM dbo.PackageStatusHistory h
                INNER JOIN dbo.Packages p ON p.PackageId = h.PackageId
                INNER JOIN dbo.Users u ON u.UserId = h.ChangedByUserId
                ORDER BY h.ChangedAtUtc DESC, h.PackageStatusHistoryId DESC;";

            var results = new List<RecentActivityItem>();

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@Count", DbType.Int32, count);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var fromOrdinal = reader.GetOrdinal("FromStatus");
                        results.Add(new RecentActivityItem
                        {
                            F20Identifier = reader.GetString(reader.GetOrdinal("F20Identifier")),
                            FromStatus = reader.IsDBNull(fromOrdinal) ? null : reader.GetString(fromOrdinal),
                            ToStatus = reader.GetString(reader.GetOrdinal("ToStatus")),
                            ChangedByUsername = reader.GetString(reader.GetOrdinal("Username")),
                            ChangedAtUtc = reader.GetDateTime(reader.GetOrdinal("ChangedAtUtc"))
                        });
                    }
                }
            }

            return results;
        }
    }
}