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
    /// The payment status SQL (T41) against the real LocalDB database. Needs db/schema.sql run since T41, which adds the
    /// PaymentStatusUpdatedAtUtc and PaymentStatusUpdatedByUserId columns. Every test is rolled back.
    /// If LocalDB isn't available these report Inconclusive.
    /// </summary>
    [TestClass]
    public class PackagePaymentRepositoryIntegrationTests
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
        public void NewPackage_HasNoUpdateYet()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);

                var info = new PackagePaymentRepository(_connectionFactory).GetPaymentInfo(packageId, unitOfWork);

                Assert.IsNotNull(info);
                Assert.AreEqual("Unpaid", info.PaymentStatus);
                Assert.IsNull(info.UpdatedAtUtc);
                Assert.IsNull(info.UpdatedBy);
            }
        }

        [TestMethod]
        public void Update_StoresStatusTimeAndStaffMember()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);
                var userId = Scalar(unitOfWork, "SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId;");
                var username = (string)ScalarObject(unitOfWork, "SELECT Username FROM dbo.Users WHERE UserId = @Value;", userId);
                var repository = new PackagePaymentRepository(_connectionFactory);

                Assert.IsTrue(repository.UpdatePaymentStatus(packageId, "Paid", userId, unitOfWork));

                var info = repository.GetPaymentInfo(packageId, unitOfWork);
                Assert.AreEqual("Paid", info.PaymentStatus);
                Assert.IsNotNull(info.UpdatedAtUtc);
                Assert.IsTrue(info.UpdatedAtUtc.Value > DateTime.UtcNow.AddMinutes(-5), "time set by the database");
                Assert.AreEqual(username, info.UpdatedBy);
            }
        }

        [TestMethod]
        public void UnknownPackage_IsNotUpdated()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var repository = new PackagePaymentRepository(_connectionFactory);
                var userId = Scalar(unitOfWork, "SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId;");

                Assert.IsFalse(repository.UpdatePaymentStatus(-1, "Paid", userId, unitOfWork));
                Assert.IsNull(repository.GetPaymentInfo(-1, unitOfWork));
            }
        }

        private static int InsertPackage(IUnitOfWork unitOfWork)
        {
            var recipientId = new PackageRegistrationRepository().InsertRecipient(new Recipient { FullName = "Payment Test" }, unitOfWork);
            var userId = Scalar(unitOfWork, "SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId;");

            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = @"INSERT INTO dbo.Packages (F20Identifier, RecipientId, Classification, Fee, PaymentStatus, CreatedByUserId)
                                        VALUES (@Id, @RecipientId, 'Personal', 10.00, 'Unpaid', @UserId);
                                        SELECT CAST(SCOPE_IDENTITY() AS int);";
                command.AddParameter("@Id", DbType.String, "TEST-PAY-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                command.AddParameter("@RecipientId", DbType.Int32, recipientId);
                command.AddParameter("@UserId", DbType.Int32, userId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static int Scalar(IUnitOfWork unitOfWork, string sql, int? value = null)
        {
            return Convert.ToInt32(ScalarObject(unitOfWork, sql, value));
        }

        private static object ScalarObject(IUnitOfWork unitOfWork, string sql, int? value = null)
        {
            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = sql;
                if (value.HasValue)
                {
                    command.AddParameter("@Value", DbType.Int32, value.Value);
                }

                return command.ExecuteScalar();
            }
        }
    }
}
