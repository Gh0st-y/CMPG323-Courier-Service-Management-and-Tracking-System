using System;
using System.Collections.Generic;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class PackageStatusHistoryRepository : IPackageStatusHistoryRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public PackageStatusHistoryRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>INSERT only — history rows are never edited or removed.</summary>
        public void Insert(PackageStatusHistoryEntry entry, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                INSERT INTO dbo.PackageStatusHistory (PackageId, FromStatus, ToStatus, ChangedByUserId, Notes)
                VALUES (@PackageId, @FromStatus, @ToStatus, @ChangedByUserId, @Notes);";

            IDbConnection ownedConnection = null;
            try
            {
                IDbCommand command;
                if (unitOfWork != null)
                {
                    command = unitOfWork.Connection.CreateCommand();
                    command.Transaction = unitOfWork.Transaction;
                }
                else
                {
                    ownedConnection = _connectionFactory.CreateOpenConnection();
                    command = ownedConnection.CreateCommand();
                }

                using (command)
                {
                    command.CommandText = sql;
                    command.AddParameter("@PackageId", DbType.Int32, entry.PackageId);
                    command.AddParameter("@FromStatus", DbType.String, entry.FromStatus.HasValue ? entry.FromStatus.Value.ToString() : null);
                    command.AddParameter("@ToStatus", DbType.String, entry.ToStatus.ToString());
                    command.AddParameter("@ChangedByUserId", DbType.Int32, entry.ChangedByUserId);
                    command.AddParameter("@Notes", DbType.String, entry.Notes);
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                ownedConnection?.Dispose();
            }
        }

        public IEnumerable<PackageStatusHistoryEntry> GetByPackageId(int packageId)
        {
            const string sql = @"
                SELECT PackageStatusHistoryId, PackageId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc, Notes
                FROM dbo.PackageStatusHistory
                WHERE PackageId = @PackageId
                ORDER BY ChangedAtUtc ASC, PackageStatusHistoryId ASC;";

            var results = new List<PackageStatusHistoryEntry>();

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@PackageId", DbType.Int32, packageId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(Map(reader));
                    }
                }
            }

            return results;
        }

        private static PackageStatusHistoryEntry Map(IDataRecord record)
        {
            var fromOrdinal = record.GetOrdinal("FromStatus");
            var notesOrdinal = record.GetOrdinal("Notes");

            return new PackageStatusHistoryEntry
            {
                PackageStatusHistoryId = record.GetInt32(record.GetOrdinal("PackageStatusHistoryId")),
                PackageId = record.GetInt32(record.GetOrdinal("PackageId")),
                FromStatus = record.IsDBNull(fromOrdinal)
                    ? (PackageStatus?)null
                    : (PackageStatus)Enum.Parse(typeof(PackageStatus), record.GetString(fromOrdinal)),
                ToStatus = (PackageStatus)Enum.Parse(typeof(PackageStatus), record.GetString(record.GetOrdinal("ToStatus"))),
                ChangedByUserId = record.GetInt32(record.GetOrdinal("ChangedByUserId")),
                ChangedAtUtc = record.GetDateTime(record.GetOrdinal("ChangedAtUtc")),
                Notes = record.IsDBNull(notesOrdinal) ? null : record.GetString(notesOrdinal)
            };
        }
    }
}
