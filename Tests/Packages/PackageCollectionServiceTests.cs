using System;
using System.Collections.Generic;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Packages
{
    [TestClass]
    public class PackageCollectionServiceTests
    {
        private sealed class FakeStatusService : IPackageStatusService
        {
            public StatusChangeResult Result { get; set; }
            public int Calls { get; private set; }
            public string F20Identifier { get; private set; }
            public PackageStatus NewStatus { get; private set; }
            public int? StorageLocationId { get; private set; }
            public int ChangedByUserId { get; private set; }
            public int? CollectedByUserId { get; private set; }

            public StatusChangeResult ChangeStatus(string f20Identifier, PackageStatus newStatus, int? storageLocationId, int changedByUserId, string notes = null, int? collectedByUserId = null)
            {
                Calls++;
                F20Identifier = f20Identifier;
                NewStatus = newStatus;
                StorageLocationId = storageLocationId;
                ChangedByUserId = changedByUserId;
                CollectedByUserId = collectedByUserId;
                return Result;
            }
        }

        private sealed class FakePackageRepository : IPackageRepository
        {
            public Package Stored { get; set; }
            public int Reads { get; private set; }

            public Package GetByF20Identifier(string f20Identifier) { Reads++; return Stored; }
            public Package GetById(int packageId) { throw new NotSupportedException(); }
            public int Insert(Package package, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public bool TryUpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, int? collectedByUserId, byte[] expectedRowVersion, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public PagedResult<Package> Search(PackageSearchCriteria criteria) { throw new NotSupportedException(); }
        }

        private sealed class FakeUserRepository : IUserRepository
        {
            public Dictionary<int, User> Users { get; } = new Dictionary<int, User>();
            public int Lookups { get; private set; }

            public User GetById(int userId)
            {
                Lookups++;
                User user;
                return Users.TryGetValue(userId, out user) ? user : null;
            }

            public User GetByUsername(string username) { throw new NotSupportedException(); }
            public int Insert(User user, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public void UpdateLastLogin(int userId, DateTime loginTimeUtc, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
        }

        private const int Processor = 4;
        private static readonly DateTime StoredTime = new DateTime(2026, 10, 5, 14, 30, 0, DateTimeKind.Utc);

        private FakeStatusService _status;
        private FakePackageRepository _packages;
        private FakeUserRepository _users;
        private PackageCollectionService _service;

        [TestInitialize]
        public void Setup()
        {
            _status = new FakeStatusService
            {
                Result = StatusChangeResult.Changed("F20-0002", PackageStatus.ReadyForCollection, PackageStatus.Collected)
            };
            _packages = new FakePackageRepository
            {
                Stored = new Package { F20Identifier = "F20-0002", Status = PackageStatus.Collected, CollectedAtUtc = StoredTime }
            };
            _users = new FakeUserRepository();
            _users.Users[9] = new User { UserId = 9, Username = "supervisor.demo", IsActive = true };
            _users.Users[10] = new User { UserId = 10, Username = "left.the.company", IsActive = false };

            _service = new PackageCollectionService(_status, _packages, _users);
        }

        [TestMethod]
        public void Collecting_GoesThroughTheStateMachine_AsACollection()
        {
            var result = _service.Collect("F20-0002", Processor);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, _status.Calls);
            Assert.AreEqual("F20-0002", _status.F20Identifier);
            Assert.AreEqual(PackageStatus.Collected, _status.NewStatus);
            Assert.IsNull(_status.StorageLocationId);
            Assert.AreEqual(Processor, _status.ChangedByUserId);
        }

        [TestMethod]
        public void NoVerifierGiven_TheProcessorIsTheVerifier_AndNoUserLookupIsNeeded()
        {
            _service.Collect("F20-0002", Processor);

            Assert.AreEqual(Processor, _status.CollectedByUserId);
            Assert.AreEqual(0, _users.Lookups);
        }

        [TestMethod]
        public void ADifferentActiveVerifier_IsRecorded_WhileTheProcessorStaysTheProcessor()
        {
            _service.Collect("F20-0002", Processor, 9);

            Assert.AreEqual(9, _status.CollectedByUserId);
            Assert.AreEqual(Processor, _status.ChangedByUserId);
        }

        [TestMethod]
        public void TheVerifierBeingTheProcessor_NeedsNoLookup()
        {
            _service.Collect("F20-0002", Processor, Processor);

            Assert.AreEqual(Processor, _status.CollectedByUserId);
            Assert.AreEqual(0, _users.Lookups);
        }

        [TestMethod]
        public void UnknownVerifier_IsRejected_AndNothingIsChanged()
        {
            var result = _service.Collect("F20-0002", Processor, 999);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(StatusChangeOutcome.InvalidInput, result.Change.Outcome);
            Assert.AreEqual("ValidationError", result.Change.ErrorCode);
            Assert.AreEqual(0, _status.Calls);
            Assert.IsNull(result.CollectedAtUtc);
        }

        [TestMethod]
        public void InactiveVerifier_IsRejected_AndNothingIsChanged()
        {
            var result = _service.Collect("F20-0002", Processor, 10);

            Assert.AreEqual(StatusChangeOutcome.InvalidInput, result.Change.Outcome);
            Assert.AreEqual(0, _status.Calls);
        }

        [TestMethod]
        public void OnSuccess_TheTimeReported_IsTheOneTheDatabaseStored()
        {
            var result = _service.Collect("F20-0002", Processor);

            Assert.AreEqual(StoredTime, result.CollectedAtUtc);
            Assert.AreEqual(1, _packages.Reads);
        }

        [TestMethod]
        public void NotReadyForCollection_IsRejectedByTheStateMachine_WithNoTimeAndNoReadBack()
        {
            _status.Result = StatusChangeResult.InvalidTransition("F20-0002", PackageStatus.InStorage, PackageStatus.Collected);

            var result = _service.Collect("F20-0002", Processor);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(StatusChangeOutcome.InvalidTransition, result.Change.Outcome);
            Assert.AreEqual("Package cannot move to Collected from In Storage.", result.Change.Message);
            Assert.IsNull(result.CollectedAtUtc);
            Assert.AreEqual(0, _packages.Reads);
        }

        [TestMethod]
        public void UnknownPackage_AndConflicts_ComeThroughUnchanged()
        {
            _status.Result = StatusChangeResult.NotFound("F20-0002", PackageStatus.Collected);
            Assert.AreEqual(StatusChangeOutcome.NotFound, _service.Collect("F20-0002", Processor).Change.Outcome);

            _status.Result = StatusChangeResult.ConcurrentUpdate("F20-0002", PackageStatus.ReadyForCollection, PackageStatus.Collected);
            Assert.AreEqual(StatusChangeOutcome.ConcurrentUpdate, _service.Collect("F20-0002", Processor).Change.Outcome);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void BlankIdentifier_Throws(string identifier)
        {
            Assert.ThrowsExactly<ArgumentException>(() => _service.Collect(identifier, Processor));
        }

        [TestMethod]
        public void Constructor_NullDependencies_Throw()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageCollectionService(null, _packages, _users));
            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageCollectionService(_status, null, _users));
            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageCollectionService(_status, _packages, null));
        }
    }
}
