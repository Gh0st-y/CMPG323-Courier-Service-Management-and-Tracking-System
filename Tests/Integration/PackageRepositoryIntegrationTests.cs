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
    /// Exercises the T05 repositories against a real LocalDB instance running db/schema.sql,
    /// so "GetByF20Identifier is a working example" is actually verified, not just compiled.
    ///
    /// Requires (localdb)\MSSQLLocalDB with the CourierService database created from
    /// db/schema.sql. If that's not available (e.g. a CI agent with no LocalDB), every test
    /// here reports Inconclusive instead of failing the build.
    /// </summary>
    [TestClass]
    public class PackageRepositoryIntegrationTests
    {
        private const string ConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=CourierService;Trusted_Connection=True;";

        private IDbConnectionFactory _connectionFactory;
        private int _userId;
        private int _recipientId;

        [TestInitialize]
        public void TestInitialize()
        {
            _connectionFactory = new SqlConnectionFactory(ConnectionString);

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                }
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"LocalDB CourierService database is not available: {ex.Message}");
            }

            _userId = SeedTestUser();
            _recipientId = SeedTestRecipient();
        }

        [TestMethod]
        public void GetByF20Identifier_ReturnsPackageWithRecipient_ForAKnownIdentifier()
        {
            var packageRepository = new PackageRepository(_connectionFactory);
            var f20Identifier = "TEST-" + Guid.NewGuid().ToString("N").Substring(0, 10);

            var packageId = packageRepository.Insert(new Package
            {
                F20Identifier = f20Identifier,
                RecipientId = _recipientId,
                SenderName = "Test Sender",
                PackageType = "Envelope",
                Classification = "Personal",
                Fee = 10.00m,
                PaymentStatus = "Unpaid",
                Status = PackageStatus.Registered,
                CreatedByUserId = _userId
            });

            var found = packageRepository.GetByF20Identifier(f20Identifier);

            Assert.IsNotNull(found);
            Assert.AreEqual(packageId, found.PackageId);
            Assert.AreEqual(f20Identifier, found.F20Identifier);
            Assert.AreEqual(PackageStatus.Registered, found.Status);
            Assert.IsNotNull(found.Recipient);
            Assert.AreEqual(_recipientId, found.Recipient.RecipientId);
        }

        [TestMethod]
        public void GetByF20Identifier_ReturnsNull_ForAnUnknownIdentifier()
        {
            var packageRepository = new PackageRepository(_connectionFactory);

            var found = packageRepository.GetByF20Identifier("F20-DOES-NOT-EXIST");

            Assert.IsNull(found);
        }

        [TestMethod]
        public void UpdateStatus_AndAuditLogInsert_CommitTogetherInOneUnitOfWork()
        {
            var packageRepository = new PackageRepository(_connectionFactory);
            var auditLogRepository = new AuditLogRepository(_connectionFactory);
            var unitOfWorkFactory = new UnitOfWorkFactory(_connectionFactory);

            var f20Identifier = "TEST-" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var packageId = packageRepository.Insert(new Package
            {
                F20Identifier = f20Identifier,
                RecipientId = _recipientId,
                Classification = "WorkRelated",
                Fee = 0.00m,
                PaymentStatus = "Exempt",
                Status = PackageStatus.Registered,
                CreatedByUserId = _userId
            });

            using (var unitOfWork = unitOfWorkFactory.Begin())
            {
                packageRepository.UpdateStatus(packageId, PackageStatus.InStorage, storageLocationId: null, unitOfWork: unitOfWork);
                auditLogRepository.Insert(new AuditLogEntry
                {
                    UserId = _userId,
                    Action = "PackageStatusChanged",
                    EntityType = "Package",
                    EntityId = f20Identifier,
                    Detail = "Registered -> InStorage"
                }, unitOfWork);

                unitOfWork.Commit();
            }

            var updated = packageRepository.GetByF20Identifier(f20Identifier);
            Assert.AreEqual(PackageStatus.InStorage, updated.Status);
        }

        [TestMethod]
        public void UnitOfWork_RollsBackBothChanges_WhenCommitIsNeverCalled()
        {
            var packageRepository = new PackageRepository(_connectionFactory);
            var unitOfWorkFactory = new UnitOfWorkFactory(_connectionFactory);

            var f20Identifier = "TEST-" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var packageId = packageRepository.Insert(new Package
            {
                F20Identifier = f20Identifier,
                RecipientId = _recipientId,
                Classification = "Personal",
                Fee = 10.00m,
                PaymentStatus = "Unpaid",
                Status = PackageStatus.Registered,
                CreatedByUserId = _userId
            });

            using (var unitOfWork = unitOfWorkFactory.Begin())
            {
                packageRepository.UpdateStatus(packageId, PackageStatus.ReadyForCollection, storageLocationId: null, unitOfWork: unitOfWork);
                // Deliberately not calling unitOfWork.Commit() — Dispose() must roll back.
            }

            var stillRegistered = packageRepository.GetByF20Identifier(f20Identifier);
            Assert.AreEqual(PackageStatus.Registered, stillRegistered.Status);
        }

        private int SeedTestUser()
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    DECLARE @RoleId INT = (SELECT TOP (1) RoleId FROM dbo.Roles WHERE RoleName = 'IntakeClerk');
                    DECLARE @Username NVARCHAR(100) = @UsernameParam;
                    DECLARE @ExistingUserId INT = (SELECT UserId FROM dbo.Users WHERE Username = @Username);
                    IF @ExistingUserId IS NOT NULL
                    BEGIN
                        SELECT @ExistingUserId;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO dbo.Users (Username, Email, PasswordHash, RoleId)
                        VALUES (@Username, 'test-user@f20.local', 'not-a-real-hash', @RoleId);
                        SELECT CAST(SCOPE_IDENTITY() AS int);
                    END";
                command.AddParameter("@UsernameParam", DbType.String, "test.repository.user");

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private int SeedTestRecipient()
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO dbo.Recipients (FullName, IdentifierNo, Email)
                    VALUES ('Test Recipient', 'T0000000', 'test-recipient@f20.local');
                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}
