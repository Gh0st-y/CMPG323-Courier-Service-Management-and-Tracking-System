using System;
using System.Data;
using System.Data.SqlClient;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Integration
{
    /// <summary>
    /// The notification log insert (T28) against the real LocalDB database. Every test works inside a transaction
    /// that is never committed, so nothing is left behind. If LocalDB isn't available these report Inconclusive.
    /// </summary>
    [TestClass]
    public class NotificationLogRepositoryIntegrationTests
    {
        private const string ConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=CourierService;Trusted_Connection=True;";

        private IDbConnectionFactory _connectionFactory;

        [TestInitialize]
        public void TestInitialize()
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                }
            }
            catch (Exception ex)
            {
                Assert.Inconclusive("LocalDB CourierService database is not available: " + ex.Message);
            }

            _connectionFactory = new SqlConnectionFactory(ConnectionString);
        }

        [TestMethod]
        public void Add_SavesTheAttempt_WithTheTimeFilledInByTheDatabase()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);

                new NotificationLogRepository(_connectionFactory).Add(new NotificationLogEntry
                {
                    PackageId = packageId,
                    Channel = "Email",
                    RecipientAddress = "log-test@courier.test",
                    Subject = "Your package is ready for collection",
                    Status = NotificationLogEntry.StatusFailed,
                    ErrorDetail = "The mail server could not be used (GeneralFailure, ConnectionRefused)."
                }, unitOfWork);

                Assert.AreEqual(1, Scalar(unitOfWork,
                    @"SELECT COUNT(*) FROM dbo.NotificationLog
                      WHERE PackageId = @Value AND Channel = 'Email' AND RecipientAddress = 'log-test@courier.test'
                        AND Subject = 'Your package is ready for collection' AND Status = 'Failed'
                        AND ErrorDetail LIKE 'The mail server could not be used%'
                        AND SentAtUtc > DATEADD(MINUTE, -5, SYSUTCDATETIME());", packageId));
            } // not committed: rolled back
        }

        [TestMethod]
        public void Add_CutsTextThatIsTooLong_InsteadOfFailing()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);

                new NotificationLogRepository(_connectionFactory).Add(new NotificationLogEntry
                {
                    PackageId = packageId,
                    Channel = "Email",
                    RecipientAddress = string.Empty,
                    Subject = new string('s', 250),
                    Status = NotificationLogEntry.StatusFailed,
                    ErrorDetail = new string('e', 600)
                }, unitOfWork);

                Assert.AreEqual(1, Scalar(unitOfWork,
                    @"SELECT COUNT(*) FROM dbo.NotificationLog
                      WHERE PackageId = @Value AND LEN(Subject) = 200 AND LEN(ErrorDetail) = 500 AND RecipientAddress = '';", packageId));
            }
        }

        private static int InsertPackage(IUnitOfWork unitOfWork)
        {
            var recipientId = new PackageRegistrationRepository().InsertRecipient(new Recipient { FullName = "Notification Log Test" }, unitOfWork);
            var userId = Scalar(unitOfWork, "SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId;");

            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = @"INSERT INTO dbo.Packages (F20Identifier, RecipientId, Classification, Fee, CreatedByUserId)
                                        VALUES (@Id, @RecipientId, 'WorkRelated', 0.00, @UserId);
                                        SELECT CAST(SCOPE_IDENTITY() AS int);";
                command.AddParameter("@Id", DbType.String, "TEST-LOG-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                command.AddParameter("@RecipientId", DbType.Int32, recipientId);
                command.AddParameter("@UserId", DbType.Int32, userId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static int Scalar(IUnitOfWork unitOfWork, string sql, int? value = null)
        {
            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = sql;
                if (value.HasValue)
                {
                    command.AddParameter("@Value", DbType.Int32, value.Value);
                }

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}