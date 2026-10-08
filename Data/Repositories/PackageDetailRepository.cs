using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class PackageDetailRepository : IPackageDetailRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public PackageDetailRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<PackageStatusHistoryItem> GetStatusHistory(int packageId)
        {
            const string sql = @"
                SELECT h.FromStatus, h.ToStatus, u.Username, h.ChangedAtUtc, h.Notes
                FROM dbo.PackageStatusHistory h
                INNER JOIN dbo.Users u ON u.UserId = h.ChangedByUserId
                WHERE h.PackageId = @PackageId
                ORDER BY h.ChangedAtUtc ASC, h.PackageStatusHistoryId ASC;";

            var results = new List<PackageStatusHistoryItem>();

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@PackageId", DbType.Int32, packageId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var fromOrdinal = reader.GetOrdinal("FromStatus");
                        var notesOrdinal = reader.GetOrdinal("Notes");
                        results.Add(new PackageStatusHistoryItem
                        {
                            FromStatus = reader.IsDBNull(fromOrdinal) ? null : reader.GetString(fromOrdinal),
                            ToStatus = reader.GetString(reader.GetOrdinal("ToStatus")),
                            ChangedByUsername = reader.GetString(reader.GetOrdinal("Username")),
                            ChangedAtUtc = reader.GetDateTime(reader.GetOrdinal("ChangedAtUtc")),
                            Notes = reader.IsDBNull(notesOrdinal) ? null : reader.GetString(notesOrdinal)
                        });
                    }
                }
            }

            return results;
        }

        public IEnumerable<NotificationLogItem> GetNotificationLog(int packageId)
        {
            // Cross-referenced to the package via PackageId, as required by DR-012.
            const string sql = @"
                SELECT Channel, RecipientAddress, Subject, Status, SentAtUtc
                FROM dbo.NotificationLog
                WHERE PackageId = @PackageId
                ORDER BY SentAtUtc DESC;";

            var results = new List<NotificationLogItem>();

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@PackageId", DbType.Int32, packageId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var subjectOrdinal = reader.GetOrdinal("Subject");
                        results.Add(new NotificationLogItem
                        {
                            Channel = reader.GetString(reader.GetOrdinal("Channel")),
                            RecipientAddress = reader.GetString(reader.GetOrdinal("RecipientAddress")),
                            Subject = reader.IsDBNull(subjectOrdinal) ? null : reader.GetString(subjectOrdinal),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            SentAtUtc = reader.GetDateTime(reader.GetOrdinal("SentAtUtc"))
                        });
                    }
                }
            }

            return results;
        }
    }
}
