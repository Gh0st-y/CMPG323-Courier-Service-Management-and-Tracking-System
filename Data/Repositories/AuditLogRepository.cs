using System;
using System.Collections.Generic;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AuditLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>INSERT only — never add Update/Delete methods here (SR-04, NFR-019).</summary>
        public void Insert(AuditLogEntry entry, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                INSERT INTO dbo.AuditLog (UserId, Action, EntityType, EntityId, Detail)
                VALUES (@UserId, @Action, @EntityType, @EntityId, @Detail);";

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
                    command.AddParameter("@UserId", DbType.Int32, (object)entry.UserId ?? DBNull.Value);
                    command.AddParameter("@Action", DbType.String, entry.Action);
                    command.AddParameter("@EntityType", DbType.String, entry.EntityType);
                    command.AddParameter("@EntityId", DbType.String, entry.EntityId);
                    command.AddParameter("@Detail", DbType.String, entry.Detail);
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                ownedConnection?.Dispose();
            }
        }

        public IEnumerable<AuditLogEntry> GetRecent(int take)
        {
            const string sql = "SELECT TOP (@Take) AuditLogId, UserId, Action, EntityType, EntityId, Detail, OccurredAtUtc FROM dbo.AuditLog ORDER BY OccurredAtUtc DESC;";

            var results = new List<AuditLogEntry>();

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@Take", DbType.Int32, take);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(MapEntry(reader));
                    }
                }
            }

            return results;
        }

        private static AuditLogEntry MapEntry(IDataRecord record)
        {
            var userIdOrdinal = record.GetOrdinal("UserId");
            var entityTypeOrdinal = record.GetOrdinal("EntityType");
            var entityIdOrdinal = record.GetOrdinal("EntityId");
            var detailOrdinal = record.GetOrdinal("Detail");

            return new AuditLogEntry
            {
                AuditLogId = record.GetInt64(record.GetOrdinal("AuditLogId")),
                UserId = record.IsDBNull(userIdOrdinal) ? (int?)null : record.GetInt32(userIdOrdinal),
                Action = record.GetString(record.GetOrdinal("Action")),
                EntityType = record.IsDBNull(entityTypeOrdinal) ? null : record.GetString(entityTypeOrdinal),
                EntityId = record.IsDBNull(entityIdOrdinal) ? null : record.GetString(entityIdOrdinal),
                Detail = record.IsDBNull(detailOrdinal) ? null : record.GetString(detailOrdinal),
                OccurredAtUtc = record.GetDateTime(record.GetOrdinal("OccurredAtUtc"))
            };
        }
    }
}
