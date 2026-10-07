using System;
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

        // "Today" is the current calendar day in South Africa (SAST, UTC+2, no daylight saving), not the UTC day,
        // so packages received just after local midnight count towards the right day. If the machine has no such
        // Windows time zone, fall back to the server's own local zone.
        private const string LocalTimeZoneId = "South Africa Standard Time";
        private static readonly TimeZoneInfo LocalTimeZone = ResolveLocalTimeZone();

        private static TimeZoneInfo ResolveLocalTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(LocalTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Local;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Local;
            }
        }

        public DashboardStats GetStats(string period = "today")
        {
            // today = current local day. week = last 7 days including today. month = last 30 days.
            // ReceivedToday and CollectedToday hold the totals for the whole period, whichever one was asked for.
            var window = DashboardPeriodWindow.For(period, DateTime.UtcNow, LocalTimeZone);

            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM dbo.Packages
                        WHERE CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc) AS ReceivedInPeriod,
                    (SELECT COUNT(*) FROM dbo.Packages WHERE Status = 'ReadyForCollection') AS ReadyForCollection,
                    (SELECT COUNT(*) FROM dbo.Packages
                        WHERE CollectedAtUtc >= @FromUtc AND CollectedAtUtc < @ToUtc) AS CollectedInPeriod,
                    (SELECT COUNT(*) FROM dbo.Packages
                        WHERE Status IN ('Registered', 'InStorage', 'ReadyForCollection')) AS Outstanding;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@FromUtc", DbType.DateTime2, window.StartUtc);
                command.AddParameter("@ToUtc", DbType.DateTime2, window.EndUtc);

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