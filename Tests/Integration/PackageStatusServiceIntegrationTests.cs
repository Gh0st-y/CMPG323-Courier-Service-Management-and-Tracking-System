using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Integration
{
    /// <summary>
    /// Runs PackageStatusService against a real LocalDB database running db/schema.sql. The things that matter most
    /// here (one transaction for status + history + audit, the RowVersion concurrency check) are database
    /// behaviour, so fakes can't prove them. If LocalDB isn't available these report Inconclusive instead of failing.
    /// Rows the tests create are removed afterwards; audit rows are left alone, the audit log is append-only.
    /// </summary>
    [TestClass]
    public class PackageStatusServiceIntegrationTests
    {
        private const string ConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=CourierService;Trusted_Connection=True;";

        private IDbConnectionFactory _connectionFactory;
        private PackageRepository _packages;
        private PackageStatusHistoryRepository _history;
        private UnitOfWorkFactory _unitOfWorkFactory;
        private int _userId;
        private int _recipientId;
        private readonly List<int> _packageIds = new List<int>();
        private readonly List<int> _locationIds = new List<int>();

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
                Assert.Inconclusive("LocalDB CourierService database is not available: " + ex.Message);
            }

            _packages = new PackageRepository(_connectionFactory);
            _history = new PackageStatusHistoryRepository(_connectionFactory);
            _unitOfWorkFactory = new UnitOfWorkFactory(_connectionFactory);

            _userId = SeedTestUser();
            _recipientId = Scalar(@"INSERT INTO dbo.Recipients (FullName, IdentifierNo, Email)
                                    VALUES ('Status Test Recipient', 'T0000000', 'status-test@f20.local');
                                    SELECT CAST(SCOPE_IDENTITY() AS int);");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (_connectionFactory == null || _packages == null)
            {
                return;
            }

            foreach (var id in _packageIds)
            {
                Exec("DELETE FROM dbo.PackageStatusHistory WHERE PackageId = @Id", id);
                Exec("DELETE FROM dbo.Packages WHERE PackageId = @Id", id);
            }

            Exec("DELETE FROM dbo.Recipients WHERE RecipientId = @Id", _recipientId);

            foreach (var id in _locationIds)
            {
                Exec("DELETE FROM dbo.StorageLocations WHERE StorageLocationId = @Id", id);
            }
        }

        // ---------------------------------------------------------------------------------

        private PackageStatusService NewService(IPackageRepository packages = null, params IPackageStatusChangeListener[] listeners)
        {
            return new PackageStatusService(
                packages ?? _packages,
                _history,
                new StorageLocationRepository(_connectionFactory),
                new AuditLogger(new AuditLogRepository(_connectionFactory)),
                _unitOfWorkFactory,
                listeners);
        }

        private Package CreatePackage(PackageStatus status, int? storageLocationId = null)
        {
            var identifier = "TEST-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var id = _packages.Insert(new Package
            {
                F20Identifier = identifier,
                RecipientId = _recipientId,
                Classification = "Personal",
                Fee = 10.00m,
                PaymentStatus = "Unpaid",
                Status = status,
                StorageLocationId = storageLocationId,
                CreatedByUserId = _userId
            });
            _packageIds.Add(id);
            return _packages.GetById(id);
        }

        private int CreateLocation()
        {
            var id = new StorageLocationRepository(_connectionFactory).Insert(new StorageLocation
            {
                Code = "TEST-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                IsActive = true
            });
            _locationIds.Add(id);
            return id;
        }

        private int AuditCount(string f20Identifier)
        {
            return Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityType = 'Package' AND EntityId = @Text", f20Identifier);
        }

        private int SeedTestUser()
        {
            return Scalar(@"
                DECLARE @RoleId INT = (SELECT TOP (1) RoleId FROM dbo.Roles WHERE RoleName = 'IntakeClerk');
                DECLARE @Existing INT = (SELECT UserId FROM dbo.Users WHERE Username = 'test.repository.user');
                IF @Existing IS NOT NULL SELECT @Existing;
                ELSE
                BEGIN
                    INSERT INTO dbo.Users (Username, Email, PasswordHash, RoleId)
                    VALUES ('test.repository.user', 'test-user@f20.local', 'not-a-real-hash', @RoleId);
                    SELECT CAST(SCOPE_IDENTITY() AS int);
                END");
        }

        private int Scalar(string sql, object parameter = null)
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, parameter);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void Exec(string sql, object parameter)
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, parameter);
                command.ExecuteNonQuery();
            }
        }

        private static void AddParameter(IDbCommand command, object parameter)
        {
            if (parameter == null)
            {
                return;
            }

            if (parameter is int)
            {
                command.AddParameter("@Id", DbType.Int32, parameter);
            }
            else
            {
                command.AddParameter("@Text", DbType.String, parameter);
            }
        }

        private sealed class ThrowingListener : IPackageStatusChangeListener
        {
            public void OnStatusChanged(PackageStatusChange change, IUnitOfWork unitOfWork)
            {
                throw new InvalidOperationException("listener failed");
            }
        }

        private sealed class RecordingListener : IPackageStatusChangeListener
        {
            public List<PackageStatusChange> Changes { get; } = new List<PackageStatusChange>();

            public void OnStatusChanged(PackageStatusChange change, IUnitOfWork unitOfWork)
            {
                Changes.Add(change);
            }
        }

        /// <summary>
        /// Reads a package normally, then, before returning it to the service, lets "someone else" change it.
        /// That is exactly the window two people acting at the same moment would hit.
        /// </summary>
        private sealed class RacingPackageRepository : IPackageRepository
        {
            private readonly IPackageRepository _inner;
            private readonly PackageStatus _competitorMovesTo;
            public bool CompetitorWon { get; private set; }

            public RacingPackageRepository(IPackageRepository inner, PackageStatus competitorMovesTo)
            {
                _inner = inner;
                _competitorMovesTo = competitorMovesTo;
            }

            public Package GetByF20Identifier(string f20Identifier)
            {
                var package = _inner.GetByF20Identifier(f20Identifier);
                CompetitorWon = _inner.TryUpdateStatus(package.PackageId, _competitorMovesTo, null, null, package.RowVersion);
                return package; // still carries the now out-of-date RowVersion
            }

            public Package GetById(int packageId) { return _inner.GetById(packageId); }
            public int Insert(Package package, IUnitOfWork unitOfWork = null) { return _inner.Insert(package, unitOfWork); }
            public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null) { _inner.UpdateStatus(packageId, newStatus, storageLocationId, unitOfWork); }
            public PagedResult<Package> Search(PackageSearchCriteria criteria) { return _inner.Search(criteria); }
            public bool TryUpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, int? collectedByUserId, byte[] expectedRowVersion, IUnitOfWork unitOfWork = null)
            {
                return _inner.TryUpdateStatus(packageId, newStatus, storageLocationId, collectedByUserId, expectedRowVersion, unitOfWork);
            }
        }

        // ---------------------------------------------------------------------------------

        [TestMethod]
        public void AllowedChange_UpdatesStatus_AndWritesHistoryAndAudit_Together()
        {
            var package = CreatePackage(PackageStatus.Registered);

            var result = NewService().ChangeStatus(package.F20Identifier, PackageStatus.InStorage, null, _userId, "shelf check");

            Assert.IsTrue(result.Success);

            var after = _packages.GetById(package.PackageId);
            Assert.AreEqual(PackageStatus.InStorage, after.Status);
            Assert.IsFalse(after.RowVersion.SequenceEqual(package.RowVersion), "Any change must move the RowVersion on.");

            var history = _history.GetByPackageId(package.PackageId).Single();
            Assert.AreEqual(PackageStatus.Registered, history.FromStatus);
            Assert.AreEqual(PackageStatus.InStorage, history.ToStatus);
            Assert.AreEqual(_userId, history.ChangedByUserId);
            Assert.AreEqual("shelf check", history.Notes);

            Assert.AreEqual(1, AuditCount(package.F20Identifier));
        }

        [TestMethod]
        public void WalkingTheWholeLifecycle_ReachesCollected_AndRecordsWhoAndWhen()
        {
            var package = CreatePackage(PackageStatus.Registered);
            var service = NewService();

            Assert.IsTrue(service.ChangeStatus(package.F20Identifier, PackageStatus.InStorage, null, _userId).Success);
            Assert.IsTrue(service.ChangeStatus(package.F20Identifier, PackageStatus.ReadyForCollection, null, _userId).Success);
            Assert.IsTrue(service.ChangeStatus(package.F20Identifier, PackageStatus.Collected, null, _userId).Success);

            var after = _packages.GetById(package.PackageId);
            Assert.AreEqual(PackageStatus.Collected, after.Status);
            Assert.AreEqual(_userId, after.CollectedByUserId);
            Assert.IsNotNull(after.CollectedAtUtc);

            var timeline = _history.GetByPackageId(package.PackageId).ToList();
            CollectionAssert.AreEqual(
                new[] { PackageStatus.InStorage, PackageStatus.ReadyForCollection, PackageStatus.Collected },
                timeline.Select(h => h.ToStatus).ToArray());
            Assert.AreEqual(3, AuditCount(package.F20Identifier));
        }

        [TestMethod]
        public void RejectedChange_LeavesThePackage_TheHistory_AndTheAuditLogUntouched()
        {
            var package = CreatePackage(PackageStatus.Registered);

            var result = NewService().ChangeStatus(package.F20Identifier, PackageStatus.Collected, null, _userId);

            Assert.AreEqual(StatusChangeOutcome.InvalidTransition, result.Outcome);
            Assert.AreEqual("Package cannot move to Collected from Registered.", result.Message);

            var after = _packages.GetById(package.PackageId);
            Assert.AreEqual(PackageStatus.Registered, after.Status);
            CollectionAssert.AreEqual(package.RowVersion, after.RowVersion);
            Assert.AreEqual(0, _history.GetByPackageId(package.PackageId).Count());
            Assert.AreEqual(0, AuditCount(package.F20Identifier));
        }

        [TestMethod]
        public void UnknownIdentifier_ReturnsNotFound()
        {
            var result = NewService().ChangeStatus("TEST-DOES-NOT-EXIST", PackageStatus.InStorage, null, _userId);

            Assert.AreEqual(StatusChangeOutcome.NotFound, result.Outcome);
        }

        // ---- DR-010: all or nothing ---------------------------------------------------------

        [TestMethod]
        public void IfALaterStepFails_TheStatusChange_TheHistory_AndTheAudit_AreAllRolledBack()
        {
            var package = CreatePackage(PackageStatus.Registered);
            var service = NewService(null, new ThrowingListener());

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => service.ChangeStatus(package.F20Identifier, PackageStatus.InStorage, null, _userId));

            Assert.AreEqual("listener failed", ex.Message);

            // By the time the listener ran, the status update, history row and audit entry had all been written
            // inside the transaction. None of it may be visible now.
            var after = _packages.GetById(package.PackageId);
            Assert.AreEqual(PackageStatus.Registered, after.Status, "The status update must be rolled back.");
            CollectionAssert.AreEqual(package.RowVersion, after.RowVersion);
            Assert.AreEqual(0, _history.GetByPackageId(package.PackageId).Count(), "The history row must be rolled back.");
            Assert.AreEqual(0, AuditCount(package.F20Identifier), "The audit entry must be rolled back.");
        }

        [TestMethod]
        public void Listener_RunsOnlyAfterTheChangeIsFullyWritten_AndSeesTheRightChange()
        {
            var package = CreatePackage(PackageStatus.Registered);
            var listener = new RecordingListener();

            NewService(null, listener).ChangeStatus(package.F20Identifier, PackageStatus.InStorage, null, _userId);

            var change = listener.Changes.Single();
            Assert.AreEqual(package.PackageId, change.PackageId);
            Assert.AreEqual(PackageStatus.Registered, change.FromStatus);
            Assert.AreEqual(PackageStatus.InStorage, change.ToStatus);
        }

        // ---- NFR-023: two people at once --------------------------------------------------

        [TestMethod]
        public void TheSecondWriterWithAStaleRowVersion_LosesAndChangesNothing()
        {
            var package = CreatePackage(PackageStatus.Registered);
            var staleVersion = package.RowVersion;

            var firstWon = _packages.TryUpdateStatus(package.PackageId, PackageStatus.InStorage, null, null, staleVersion);
            var secondWon = _packages.TryUpdateStatus(package.PackageId, PackageStatus.InStorage, null, null, staleVersion);

            Assert.IsTrue(firstWon);
            Assert.IsFalse(secondWon, "The row had moved on, so a write based on the old RowVersion must not match.");
            Assert.AreEqual(PackageStatus.InStorage, _packages.GetById(package.PackageId).Status);
        }

        [TestMethod]
        public void IfSomeoneChangesItBetweenReadAndSave_TheServiceReportsAConflict_AndWritesNothing()
        {
            var package = CreatePackage(PackageStatus.Registered);
            var racing = new RacingPackageRepository(_packages, PackageStatus.InStorage);
            var listener = new RecordingListener();

            // The competitor moves it to In Storage while we are about to do the very same thing.
            var result = NewService(racing, listener).ChangeStatus(package.F20Identifier, PackageStatus.InStorage, null, _userId);

            Assert.IsTrue(racing.CompetitorWon, "Test setup: the competing change should have gone through.");
            Assert.AreEqual(StatusChangeOutcome.ConcurrentUpdate, result.Outcome);
            Assert.AreEqual("ConcurrentUpdate", result.ErrorCode);

            Assert.AreEqual(PackageStatus.InStorage, _packages.GetById(package.PackageId).Status, "The competitor's change stands.");
            Assert.AreEqual(0, _history.GetByPackageId(package.PackageId).Count(), "The loser must not leave a history row.");
            Assert.AreEqual(0, AuditCount(package.F20Identifier), "The loser must not leave an audit entry.");
            Assert.AreEqual(0, listener.Changes.Count, "Nobody should be told about a change that didn't happen.");
        }

        [TestMethod]
        public void AfterAConflict_RetryingWorksOnceTheFreshStatusIsRead()
        {
            var package = CreatePackage(PackageStatus.Registered);
            var racing = new RacingPackageRepository(_packages, PackageStatus.InStorage);
            NewService(racing).ChangeStatus(package.F20Identifier, PackageStatus.InStorage, null, _userId);

            // "Reload and try again": now the package really is In Storage, so the next step is valid.
            var retry = NewService().ChangeStatus(package.F20Identifier, PackageStatus.ReadyForCollection, null, _userId);

            Assert.IsTrue(retry.Success);
            Assert.AreEqual(PackageStatus.ReadyForCollection, _packages.GetById(package.PackageId).Status);
        }

        // ---- storage location -----------------------------------------------------------------

        [TestMethod]
        public void StorageLocation_IsKeptWhenNoneIsGiven_AndReplacedWhenOneIs()
        {
            var shelfA = CreateLocation();
            var shelfB = CreateLocation();
            var package = CreatePackage(PackageStatus.Registered, shelfA);
            var service = NewService();

            service.ChangeStatus(package.F20Identifier, PackageStatus.InStorage, null, _userId);
            Assert.AreEqual(shelfA, _packages.GetById(package.PackageId).StorageLocationId, "No location given, so keep the current one.");

            service.ChangeStatus(package.F20Identifier, PackageStatus.ReadyForCollection, shelfB, _userId);
            Assert.AreEqual(shelfB, _packages.GetById(package.PackageId).StorageLocationId, "A location was given, so use it.");
        }

        // ---- T19 / T20 ------------------------------------------------------------------------

        private int SeedVerifier()
        {
            return Scalar(@"
                DECLARE @RoleId INT = (SELECT TOP (1) RoleId FROM dbo.Roles WHERE RoleName = 'Supervisor');
                DECLARE @Existing INT = (SELECT UserId FROM dbo.Users WHERE Username = 'test.verifier.user');
                IF @Existing IS NOT NULL SELECT @Existing;
                ELSE
                BEGIN
                    INSERT INTO dbo.Users (Username, Email, PasswordHash, RoleId)
                    VALUES ('test.verifier.user', 'test-verifier@f20.local', 'not-a-real-hash', @RoleId);
                    SELECT CAST(SCOPE_IDENTITY() AS int);
                END");
        }

        [TestMethod]
        public void Lookup_ReturnsTheStorageLocationCode_AndTheRecipient()
        {
            var locationId = CreateLocation();
            var code = new StorageLocationRepository(_connectionFactory).GetById(locationId).Code;
            var package = CreatePackage(PackageStatus.InStorage, locationId);

            var found = new PackageLookupService(_packages).Find("  " + package.F20Identifier + "\r\n");

            Assert.IsNotNull(found);
            Assert.AreEqual(package.PackageId, found.PackageId);
            Assert.AreEqual(code, found.StorageLocationCode);
            Assert.AreEqual("Status Test Recipient", found.Recipient.FullName);
        }

        [TestMethod]
        public void Lookup_ForAPackageWithNoLocation_HasANullCode_NotAnError()
        {
            var package = CreatePackage(PackageStatus.Registered);

            var found = new PackageLookupService(_packages).Find(package.F20Identifier);

            Assert.IsNotNull(found);
            Assert.IsNull(found.StorageLocationId);
            Assert.IsNull(found.StorageLocationCode);
        }

        [TestMethod]
        public void Lookup_OfAnUnknownOrMalformedCode_IsNull_NotAnException()
        {
            var service = new PackageLookupService(_packages);

            Assert.IsNull(service.Find("TEST-DOES-NOT-EXIST"));
            Assert.IsNull(service.Find("x'; DROP TABLE dbo.Packages;--"));
        }

        [TestMethod]
        public void Collecting_RecordsTheVerifier_TheProcessor_AndTheTime_Together()
        {
            var verifierId = SeedVerifier();
            var package = CreatePackage(PackageStatus.ReadyForCollection);
            var collection = new PackageCollectionService(NewService(), _packages, new UserRepository(_connectionFactory));

            var result = collection.Collect(package.F20Identifier, _userId, verifierId);

            Assert.IsTrue(result.Success);

            var after = _packages.GetById(package.PackageId);
            Assert.AreEqual(PackageStatus.Collected, after.Status);
            Assert.AreEqual(verifierId, after.CollectedByUserId, "The package records who verified the collector.");
            Assert.AreEqual(after.CollectedAtUtc, result.CollectedAtUtc, "The time reported is the time stored.");

            var history = _history.GetByPackageId(package.PackageId).Single();
            Assert.AreEqual(_userId, history.ChangedByUserId, "History records who processed it.");
            Assert.AreEqual(1, AuditCount(package.F20Identifier));
        }

        [TestMethod]
        public void Collecting_ThatIsNotReadyForCollection_ChangesNothing()
        {
            var package = CreatePackage(PackageStatus.InStorage);
            var collection = new PackageCollectionService(NewService(), _packages, new UserRepository(_connectionFactory));

            var result = collection.Collect(package.F20Identifier, _userId);

            Assert.AreEqual(StatusChangeOutcome.InvalidTransition, result.Change.Outcome);
            Assert.AreEqual("Package cannot move to Collected from In Storage.", result.Change.Message);

            var after = _packages.GetById(package.PackageId);
            Assert.AreEqual(PackageStatus.InStorage, after.Status);
            Assert.IsNull(after.CollectedAtUtc);
            Assert.IsNull(after.CollectedByUserId);
            Assert.AreEqual(0, _history.GetByPackageId(package.PackageId).Count());
        }

        [TestMethod]
        public void Collecting_WithAVerifierWhoDoesNotExist_IsRejected_NotADatabaseError()
        {
            var package = CreatePackage(PackageStatus.ReadyForCollection);
            var collection = new PackageCollectionService(NewService(), _packages, new UserRepository(_connectionFactory));

            var result = collection.Collect(package.F20Identifier, _userId, 2000000000);

            Assert.AreEqual(StatusChangeOutcome.InvalidInput, result.Change.Outcome);
            Assert.AreEqual(PackageStatus.ReadyForCollection, _packages.GetById(package.PackageId).Status);
        }

        [TestMethod]
        public void ChangingStatus_WithAStorageLocationThatDoesNotExist_IsRejected_NotADatabaseError()
        {
            var package = CreatePackage(PackageStatus.Registered);

            var result = NewService().ChangeStatus(package.F20Identifier, PackageStatus.InStorage, 2000000000, _userId);

            Assert.AreEqual(StatusChangeOutcome.InvalidInput, result.Outcome);
            Assert.AreEqual(PackageStatus.Registered, _packages.GetById(package.PackageId).Status);
        }
    }
}
