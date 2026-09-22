using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public NotificationRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void Enqueue(NotificationQueueItem item, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                INSERT INTO dbo.NotificationQueue (PackageId, Channel, TemplateKey, Status)
                VALUES (@PackageId, @Channel, @TemplateKey, 'Pending');";

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
                    command.AddParameter("@PackageId", DbType.Int32, item.PackageId);
                    command.AddParameter("@Channel", DbType.String, item.Channel);
                    command.AddParameter("@TemplateKey", DbType.String, item.TemplateKey);
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                ownedConnection?.Dispose();
            }
        }

        public NotificationQueueItem GetNextPending()
        {
            const string sql = @"
                SELECT TOP (1) NotificationQueueId, PackageId, Channel, TemplateKey, Status,
                       AttemptCount, EnqueuedAtUtc, LastAttemptAtUtc
                FROM dbo.NotificationQueue
                WHERE Status = 'Pending'
                ORDER BY EnqueuedAtUtc ASC;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapItem(reader) : null;
                }
            }
        }

        public void MarkSent(int notificationQueueId, IUnitOfWork unitOfWork = null)
        {
            UpdateStatus(notificationQueueId, "Sent", unitOfWork);
        }

        public void MarkFailed(int notificationQueueId, IUnitOfWork unitOfWork = null)
        {
            UpdateStatus(notificationQueueId, "Failed", unitOfWork);
        }

        private void UpdateStatus(int notificationQueueId, string status, IUnitOfWork unitOfWork)
        {
            const string sql = @"
                UPDATE dbo.NotificationQueue
                SET Status = @Status,
                    AttemptCount = AttemptCount + 1,
                    LastAttemptAtUtc = SYSUTCDATETIME()
                WHERE NotificationQueueId = @NotificationQueueId;";

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
                    command.AddParameter("@Status", DbType.String, status);
                    command.AddParameter("@NotificationQueueId", DbType.Int32, notificationQueueId);
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                ownedConnection?.Dispose();
            }
        }

        private static NotificationQueueItem MapItem(IDataRecord record)
        {
            var lastAttemptOrdinal = record.GetOrdinal("LastAttemptAtUtc");

            return new NotificationQueueItem
            {
                NotificationQueueId = record.GetInt32(record.GetOrdinal("NotificationQueueId")),
                PackageId = record.GetInt32(record.GetOrdinal("PackageId")),
                Channel = record.GetString(record.GetOrdinal("Channel")),
                TemplateKey = record.GetString(record.GetOrdinal("TemplateKey")),
                Status = record.GetString(record.GetOrdinal("Status")),
                AttemptCount = record.GetInt32(record.GetOrdinal("AttemptCount")),
                EnqueuedAtUtc = record.GetDateTime(record.GetOrdinal("EnqueuedAtUtc")),
                LastAttemptAtUtc = record.IsDBNull(lastAttemptOrdinal) ? (System.DateTime?)null : record.GetDateTime(lastAttemptOrdinal)
            };
        }
    }
}
