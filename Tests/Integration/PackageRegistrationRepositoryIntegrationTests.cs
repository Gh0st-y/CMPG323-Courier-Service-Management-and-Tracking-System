using System;
using System.Data;
using System.Data.SqlClient;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Integration
{
    /// <summary>
    /// The registration SQL against the real LocalDB database (db/schema.sql and db/seed.sql loaded).
    /// Every test works inside a transaction that is never committed, so nothing is left behind.
    /// If LocalDB isn't available these report Inconclusive instead of failing.
    /// </summary>
    [TestClass]
    public class PackageRegistrationRepositoryIntegrationTests
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
        public void NextF20Number_IsAboveEveryNumberedIdentifier_AndIgnoresOtherFormats()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var highest = Scalar(unitOfWork,
                    "SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(F20Identifier, 5, 26) AS INT)), 0) FROM dbo.Packages WHERE F20Identifier LIKE 'F20-[0-9]%';");

                // A package whose code isn't F20-digits must not break or change the numbering
                InsertPackage(unitOfWork, "TEST-REG-" + Guid.NewGuid().ToString("N").Substring(0, 8));

                var next = new PackageRegistrationRepository().NextF20Number(unitOfWork);

                Assert.AreEqual(highest + 1, next);
            } // not committed: rolled back
        }

        [TestMethod]
        public void NextF20Number_GivesANumberThatIsFree()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var identifier = PackageRegistrationService.FormatF20Identifier(new PackageRegistrationRepository().NextF20Number(unitOfWork));

                var taken = Scalar(unitOfWork, "SELECT COUNT(*) FROM dbo.Packages WHERE F20Identifier = @Value;", DbType.String, identifier);
                Assert.AreEqual(0, taken, identifier + " is already used");
            }
        }

        [TestMethod]
        public void InsertRecipient_SavesTheDetails()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var id = new PackageRegistrationRepository().InsertRecipient(new Recipient
                {
                    FullName = "Integration Test Recipient",
                    IdentifierNo = "TEST-0001",
                    Email = "integration@courier.test",
                    PhoneNumber = "0820000000",
                    Department = null
                }, unitOfWork);

                Assert.IsTrue(id > 0);
                Assert.AreEqual(1, Scalar(unitOfWork,
                    "SELECT COUNT(*) FROM dbo.Recipients WHERE RecipientId = @Value AND Email = 'integration@courier.test' AND Department IS NULL;",
                    DbType.Int32, id));
            }
        }

        [TestMethod]
        public void SeededFees_AreReadFromAppConfig()
        {
            var config = new AppConfigRepository(_connectionFactory);

            Assert.AreEqual("10.00", config.GetValue("Fee.Personal"));
            Assert.AreEqual("0.00", config.GetValue("Fee.WorkRelated"));
            Assert.IsNull(config.GetValue("No.Such.Key"));
        }

        // Runs inside the test's own transaction. Parameterised like everything else (SR-03).
        private static int Scalar(IUnitOfWork unitOfWork, string sql, DbType valueType = DbType.String, object value = null)
        {
            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = sql;
                if (value != null)
                {
                    command.AddParameter("@Value", valueType, value);
                }

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static void InsertPackage(IUnitOfWork unitOfWork, string f20Identifier)
        {
            var recipientId = new PackageRegistrationRepository().InsertRecipient(new Recipient { FullName = "Integration Test" }, unitOfWork);
            var userId = Scalar(unitOfWork, "SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId;");

            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = @"INSERT INTO dbo.Packages (F20Identifier, RecipientId, Classification, Fee, CreatedByUserId)
                                        VALUES (@Id, @RecipientId, 'Personal', 10.00, @UserId);";
                command.AddParameter("@Id", DbType.String, f20Identifier);
                command.AddParameter("@RecipientId", DbType.Int32, recipientId);
                command.AddParameter("@UserId", DbType.Int32, userId);
                command.ExecuteNonQuery();
            }
        }
    }
}