using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class NotificationLogRepository : INotificationLogRepository
    {
        // Column sizes from db/schema.sql (dbo.NotificationLog)
        public const int MaxChannel = 10;
        public const int MaxRecipientAddress = 200;
        public const int MaxSubject = 200;
        public const int MaxStatus = 20;
        public const int MaxErrorDetail = 500;

        private readonly IDbConnectionFactory _connectionFactory;

        public NotificationLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void Add(NotificationLogEntry entry, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                INSERT INTO dbo.NotificationLog (PackageId, Channel, RecipientAddress, Subject, Status, ErrorDetail)
                VALUES (@PackageId, @Channel, @RecipientAddress, @Subject, @Status, @ErrorDetail);";

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
                    command.AddParameter("@Channel", DbType.String, Cut(entry.Channel, MaxChannel) ?? string.Empty);
                    command.AddParameter("@RecipientAddress", DbType.String, Cut(entry.RecipientAddress, MaxRecipientAddress) ?? string.Empty);
                    command.AddParameter("@Subject", DbType.String, Cut(entry.Subject, MaxSubject));
                    command.AddParameter("@Status", DbType.String, Cut(entry.Status, MaxStatus) ?? string.Empty);
                    command.AddParameter("@ErrorDetail", DbType.String, Cut(entry.ErrorDetail, MaxErrorDetail));
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                ownedConnection?.Dispose();
            }
        }

        private static string Cut(string value, int max)
        {
            if (value == null)
            {
                return null;
            }

            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}