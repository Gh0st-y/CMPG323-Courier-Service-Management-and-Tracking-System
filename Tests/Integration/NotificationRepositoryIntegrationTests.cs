using System;
using System.Data;
using System.Data.SqlClient;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Integration
{
    /// <summary>
    /// The queue SQL the background worker relies on (T25), against the real LocalDB database (db/schema.sql and
    /// db/seed.sql loaded). Every test works inside a transaction that is never committed, so nothing is left behind.
    /// If LocalDB isn't available these report Inconclusive instead of failing.
    /// </summary>
    [TestClass]
    public class NotificationRepositoryIntegrationTests
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
        public void Enqueue_AddsAPendingRow_InsideTheTransaction()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);

                new NotificationRepository(_connectionFactory).Enqueue(new NotificationQueueItem
                {
                    PackageId = packageId,
                    Channel = NotificationChannels.Email,
                    TemplateKey = NotificationTemplateKeys.ReadyForCollection
                }, unitOfWork);

                Assert.AreEqual(1, Scalar(unitOfWork,
                    @"SELECT COUNT(*) FROM dbo.NotificationQueue
                      WHERE PackageId = @Value AND Channel = 'Email' AND TemplateKey = 'ReadyForCollection'
                        AND Status = 'Pending' AND AttemptCount = 0 AND LastAttemptAtUtc IS NULL;", packageId));
            } // not committed: rolled back
        }

        [TestMethod]
        public void GetNextDue_SkipsAnItemThatJustFailed_AndReturnsTheOldestDueOne()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);

                // Older than anything real, so they come first whatever else is in the queue
                var justFailed = InsertQueueRow(unitOfWork, packageId, "1999-01-01", lastAttemptNow: true);
                var due = InsertQueueRow(unitOfWork, packageId, "2000-01-01", lastAttemptNow: false);

                var next = new NotificationRepository(_connectionFactory).GetNextDue(DateTime.UtcNow.AddSeconds(-60), unitOfWork);

                Assert.IsNotNull(next);
                Assert.AreNotEqual(justFailed, next.NotificationQueueId, "a failed item waits for the retry delay");
                Assert.AreEqual(due, next.NotificationQueueId);
                Assert.AreEqual(packageId, next.PackageId);
            }
        }

        [TestMethod]
        public void RecordFailedAttempt_KeepsItPending_AndCountsTheAttempt()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);
                var id = InsertQueueRow(unitOfWork, packageId, "2000-01-01", lastAttemptNow: false);
                var repository = new NotificationRepository(_connectionFactory);

                repository.RecordFailedAttempt(id, unitOfWork);

                Assert.AreEqual(1, Scalar(unitOfWork,
                    @"SELECT COUNT(*) FROM dbo.NotificationQueue
                      WHERE NotificationQueueId = @Value AND Status = 'Pending' AND AttemptCount = 1 AND LastAttemptAtUtc IS NOT NULL;", id));

                repository.MarkFailed(id, unitOfWork);

                Assert.AreEqual(1, Scalar(unitOfWork,
                    "SELECT COUNT(*) FROM dbo.NotificationQueue WHERE NotificationQueueId = @Value AND Status = 'Failed' AND AttemptCount = 2;", id));
            }
        }

        [TestMethod]
        public void GetLatestForPackage_ReturnsTheNewestRowOnThatChannel()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);
                InsertQueueRow(unitOfWork, packageId, "2000-01-01", lastAttemptNow: false);
                var newest = InsertQueueRow(unitOfWork, packageId, "2000-01-02", lastAttemptNow: false);
                var repository = new NotificationRepository(_connectionFactory);

                var latest = repository.GetLatestForPackage(packageId, "Email", unitOfWork);

                Assert.IsNotNull(latest);
                Assert.AreEqual(newest, latest.NotificationQueueId);
                Assert.IsNull(repository.GetLatestForPackage(packageId, "SMS", unitOfWork));
            }
        }

        [TestMethod]
        public void Requeue_OnlyChangesAFailedRow_AndResetsItsAttempts()
        {
            using (var unitOfWork = new UnitOfWork(_connectionFactory))
            {
                var packageId = InsertPackage(unitOfWork);
                var id = InsertQueueRow(unitOfWork, packageId, "2000-01-01", lastAttemptNow: true);
                var repository = new NotificationRepository(_connectionFactory);

                Assert.IsFalse(repository.Requeue(id, unitOfWork), "a Pending row is left alone");

                repository.MarkFailed(id, unitOfWork);
                Assert.IsTrue(repository.Requeue(id, unitOfWork));

                Assert.AreEqual(1, Scalar(unitOfWork,
                    @"SELECT COUNT(*) FROM dbo.NotificationQueue
                      WHERE NotificationQueueId = @Value AND Status = 'Pending' AND AttemptCount = 0 AND LastAttemptAtUtc IS NULL;", id));
                Assert.IsFalse(repository.Requeue(id, unitOfWork), "a second resend does nothing");
            }
        }

        private static int InsertQueueRow(IUnitOfWork unitOfWork, int packageId, string enqueuedAtUtc, bool lastAttemptNow)
        {
            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = @"
                    INSERT INTO dbo.NotificationQueue (PackageId, Channel, TemplateKey, Status, AttemptCount, EnqueuedAtUtc, LastAttemptAtUtc)
                    VALUES (@PackageId, 'Email', 'ReadyForCollection', 'Pending', @Attempts, @EnqueuedAtUtc,
                            CASE WHEN @Attempts = 1 THEN SYSUTCDATETIME() ELSE NULL END);
                    SELECT CAST(SCOPE_IDENTITY() AS int);";
                command.AddParameter("@PackageId", DbType.Int32, packageId);
                command.AddParameter("@Attempts", DbType.Int32, lastAttemptNow ? 1 : 0);
                command.AddParameter("@EnqueuedAtUtc", DbType.DateTime2, DateTime.Parse(enqueuedAtUtc, System.Globalization.CultureInfo.InvariantCulture));
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static int InsertPackage(IUnitOfWork unitOfWork)
        {
            var recipientId = new PackageRegistrationRepository().InsertRecipient(new Recipient { FullName = "Notification Test" }, unitOfWork);
            var userId = Scalar(unitOfWork, "SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId;");

            using (var command = unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = @"INSERT INTO dbo.Packages (F20Identifier, RecipientId, Classification, Fee, CreatedByUserId)
                                        VALUES (@Id, @RecipientId, 'WorkRelated', 0.00, @UserId);
                                        SELECT CAST(SCOPE_IDENTITY() AS int);";
                command.AddParameter("@Id", DbType.String, "TEST-NTF-" + Guid.NewGuid().ToString("N").Substring(0, 8));
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