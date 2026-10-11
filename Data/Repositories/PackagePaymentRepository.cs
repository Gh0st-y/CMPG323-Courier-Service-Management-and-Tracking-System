using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using CourierService.Domain;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class PackagePaymentRepository : IPackagePaymentRepository
    {
        // SQL Server's "Invalid column name": the database hasn't had db/schema.sql run since T41 added the columns
        private const int InvalidColumnName = 207;

        private readonly IDbConnectionFactory _connectionFactory;

        public PackagePaymentRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public PaymentInfo GetPaymentInfo(int packageId, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                SELECT p.PackageId, p.PaymentStatus, p.PaymentStatusUpdatedAtUtc, u.Username AS UpdatedBy
                FROM dbo.Packages p
                LEFT JOIN dbo.Users u ON u.UserId = p.PaymentStatusUpdatedByUserId
                WHERE p.PackageId = @PackageId;";

            try
            {
                return Run(unitOfWork, sql, command =>
                {
                    command.AddParameter("@PackageId", DbType.Int32, packageId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        var updatedAtOrdinal = reader.GetOrdinal("PaymentStatusUpdatedAtUtc");
                        var updatedByOrdinal = reader.GetOrdinal("UpdatedBy");
                        return new PaymentInfo
                        {
                            PackageId = reader.GetInt32(reader.GetOrdinal("PackageId")),
                            PaymentStatus = reader.GetString(reader.GetOrdinal("PaymentStatus")),
                            UpdatedAtUtc = reader.IsDBNull(updatedAtOrdinal) ? (System.DateTime?)null : reader.GetDateTime(updatedAtOrdinal),
                            UpdatedBy = reader.IsDBNull(updatedByOrdinal) ? null : reader.GetString(updatedByOrdinal)
                        };
                    }
                });
            }
            catch (SqlException ex) when (ex.Number == InvalidColumnName && unitOfWork == null)
            {
                // Keeps the package detail page working on a database that hasn't been updated yet; only "who/when" is missing
                Trace.TraceWarning("Payment status history columns are missing: run db/schema.sql to add them (T41).");
                return null;
            }
        }

        public bool UpdatePaymentStatus(int packageId, string paymentStatus, int changedByUserId, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                UPDATE dbo.Packages
                SET PaymentStatus = @PaymentStatus,
                    PaymentStatusUpdatedAtUtc = SYSUTCDATETIME(),
                    PaymentStatusUpdatedByUserId = @ChangedByUserId
                WHERE PackageId = @PackageId;";

            return Run(unitOfWork, sql, command =>
            {
                command.AddParameter("@PaymentStatus", DbType.String, paymentStatus);
                command.AddParameter("@ChangedByUserId", DbType.Int32, changedByUserId);
                command.AddParameter("@PackageId", DbType.Int32, packageId);
                return command.ExecuteNonQuery() == 1;
            });
        }

        private T Run<T>(IUnitOfWork unitOfWork, string sql, System.Func<IDbCommand, T> body)
        {
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
                    return body(command);
                }
            }
            finally
            {
                ownedConnection?.Dispose();
            }
        }
    }
}
