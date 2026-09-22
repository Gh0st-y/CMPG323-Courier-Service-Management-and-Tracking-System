using System;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    /// <summary>
    /// Reference implementation — GetByF20Identifier is the "copy this pattern" example called
    /// out in T05: open connection, parameterised SQL, ordinal-safe mapping, dispose via using.
    /// </summary>
    public class PackageRepository : IPackageRepository
    {
        private const string SelectColumns = @"
            p.PackageId, p.F20Identifier, p.RecipientId, p.SenderName, p.PackageType, p.Classification,
            p.Fee, p.PaymentStatus, p.Status, p.StorageLocationId, p.Notes, p.CreatedByUserId,
            p.CreatedAtUtc, p.CollectedAtUtc, p.CollectedByUserId, p.RowVersion,
            r.RecipientId AS R_RecipientId, r.FullName AS R_FullName, r.IdentifierNo AS R_IdentifierNo,
            r.Email AS R_Email, r.PhoneNumber AS R_PhoneNumber, r.Department AS R_Department,
            r.CreatedAtUtc AS R_CreatedAtUtc";

        private readonly IDbConnectionFactory _connectionFactory;

        public PackageRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public Package GetByF20Identifier(string f20Identifier)
        {
            const string sql = @"
                SELECT " + SelectColumns + @"
                FROM dbo.Packages p
                INNER JOIN dbo.Recipients r ON r.RecipientId = p.RecipientId
                WHERE p.F20Identifier = @F20Identifier;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@F20Identifier", DbType.String, f20Identifier);

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapPackage(reader) : null;
                }
            }
        }

        public Package GetById(int packageId)
        {
            const string sql = @"
                SELECT " + SelectColumns + @"
                FROM dbo.Packages p
                INNER JOIN dbo.Recipients r ON r.RecipientId = p.RecipientId
                WHERE p.PackageId = @PackageId;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@PackageId", DbType.Int32, packageId);

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapPackage(reader) : null;
                }
            }
        }

        public int Insert(Package package, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                INSERT INTO dbo.Packages
                    (F20Identifier, RecipientId, SenderName, PackageType, Classification, Fee,
                     PaymentStatus, Status, StorageLocationId, Notes, CreatedByUserId)
                VALUES
                    (@F20Identifier, @RecipientId, @SenderName, @PackageType, @Classification, @Fee,
                     @PaymentStatus, @Status, @StorageLocationId, @Notes, @CreatedByUserId);
                SELECT CAST(SCOPE_IDENTITY() AS int);";

            return RunOwnedOrShared(unitOfWork, sql, command =>
            {
                command.AddParameter("@F20Identifier", DbType.String, package.F20Identifier);
                command.AddParameter("@RecipientId", DbType.Int32, package.RecipientId);
                command.AddParameter("@SenderName", DbType.String, package.SenderName);
                command.AddParameter("@PackageType", DbType.String, package.PackageType);
                command.AddParameter("@Classification", DbType.String, package.Classification);
                command.AddParameter("@Fee", DbType.Decimal, package.Fee);
                command.AddParameter("@PaymentStatus", DbType.String, package.PaymentStatus);
                command.AddParameter("@Status", DbType.String, package.Status.ToString());
                command.AddParameter("@StorageLocationId", DbType.Int32, (object)package.StorageLocationId ?? DBNull.Value);
                command.AddParameter("@Notes", DbType.String, package.Notes);
                command.AddParameter("@CreatedByUserId", DbType.Int32, package.CreatedByUserId);

                return Convert.ToInt32(command.ExecuteScalar());
            });
        }

        public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                UPDATE dbo.Packages
                SET Status = @Status,
                    StorageLocationId = COALESCE(@StorageLocationId, StorageLocationId)
                WHERE PackageId = @PackageId;";

            RunOwnedOrShared(unitOfWork, sql, command =>
            {
                command.AddParameter("@Status", DbType.String, newStatus.ToString());
                command.AddParameter("@StorageLocationId", DbType.Int32, (object)storageLocationId ?? DBNull.Value);
                command.AddParameter("@PackageId", DbType.Int32, packageId);
                command.ExecuteNonQuery();
                return 0;
            });
        }

        private static Package MapPackage(IDataRecord record)
        {
            return new Package
            {
                PackageId = record.GetInt32(record.GetOrdinal("PackageId")),
                F20Identifier = record.GetString(record.GetOrdinal("F20Identifier")),
                RecipientId = record.GetInt32(record.GetOrdinal("RecipientId")),
                SenderName = GetNullableString(record, "SenderName"),
                PackageType = GetNullableString(record, "PackageType"),
                Classification = record.GetString(record.GetOrdinal("Classification")),
                Fee = record.GetDecimal(record.GetOrdinal("Fee")),
                PaymentStatus = record.GetString(record.GetOrdinal("PaymentStatus")),
                Status = (PackageStatus)Enum.Parse(typeof(PackageStatus), record.GetString(record.GetOrdinal("Status"))),
                StorageLocationId = GetNullableInt(record, "StorageLocationId"),
                Notes = GetNullableString(record, "Notes"),
                CreatedByUserId = record.GetInt32(record.GetOrdinal("CreatedByUserId")),
                CreatedAtUtc = record.GetDateTime(record.GetOrdinal("CreatedAtUtc")),
                CollectedAtUtc = GetNullableDateTime(record, "CollectedAtUtc"),
                CollectedByUserId = GetNullableInt(record, "CollectedByUserId"),
                RowVersion = (byte[])record.GetValue(record.GetOrdinal("RowVersion")),
                Recipient = new Recipient
                {
                    RecipientId = record.GetInt32(record.GetOrdinal("R_RecipientId")),
                    FullName = record.GetString(record.GetOrdinal("R_FullName")),
                    IdentifierNo = GetNullableString(record, "R_IdentifierNo"),
                    Email = GetNullableString(record, "R_Email"),
                    PhoneNumber = GetNullableString(record, "R_PhoneNumber"),
                    Department = GetNullableString(record, "R_Department"),
                    CreatedAtUtc = record.GetDateTime(record.GetOrdinal("R_CreatedAtUtc"))
                }
            };
        }

        private static string GetNullableString(IDataRecord record, string column)
        {
            var ordinal = record.GetOrdinal(column);
            return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
        }

        private static int? GetNullableInt(IDataRecord record, string column)
        {
            var ordinal = record.GetOrdinal(column);
            return record.IsDBNull(ordinal) ? (int?)null : record.GetInt32(ordinal);
        }

        private static DateTime? GetNullableDateTime(IDataRecord record, string column)
        {
            var ordinal = record.GetOrdinal(column);
            return record.IsDBNull(ordinal) ? (DateTime?)null : record.GetDateTime(ordinal);
        }

        /// <summary>
        /// Runs <paramref name="body"/> against the shared connection/transaction when
        /// <paramref name="unitOfWork"/> is supplied (so it commits atomically with whatever
        /// else the caller is doing), otherwise opens and owns a short-lived connection.
        /// </summary>
        private int RunOwnedOrShared(IUnitOfWork unitOfWork, string commandText, Func<IDbCommand, int> body)
        {
            if (unitOfWork != null)
            {
                using (var command = unitOfWork.Connection.CreateCommand())
                {
                    command.Transaction = unitOfWork.Transaction;
                    command.CommandText = commandText;
                    return body(command);
                }
            }

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                return body(command);
            }
        }
    }
}
