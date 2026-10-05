using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Packages
{
    [TestClass]
    public class PackageStatusServiceTests
    {
        // ---------------------------------------------------------------------------------
        // Fakes. They all write to one shared Events list, so tests can check what happened and in what order.
        // ---------------------------------------------------------------------------------

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            private readonly List<string> _events;
            public FakeUnitOfWork(List<string> events) { _events = events; }

            public bool Committed { get; private set; }
            public bool Disposed { get; private set; }
            public IDbConnection Connection { get { return null; } }
            public IDbTransaction Transaction { get { return null; } }

            public void Commit() { Committed = true; _events.Add("commit"); }

            // Like the real UnitOfWork: disposing without a Commit() is a rollback.
            public void Dispose() { Disposed = true; if (!Committed) _events.Add("rollback"); }
        }

        private sealed class FakeUnitOfWorkFactory : IUnitOfWorkFactory
        {
            private readonly List<string> _events;
            public FakeUnitOfWorkFactory(List<string> events) { _events = events; }

            public List<FakeUnitOfWork> Started { get; } = new List<FakeUnitOfWork>();

            public IUnitOfWork Begin()
            {
                var uow = new FakeUnitOfWork(_events);
                Started.Add(uow);
                return uow;
            }
        }

        private sealed class FakePackageRepository : IPackageRepository
        {
            private readonly List<string> _events;
            public FakePackageRepository(List<string> events) { _events = events; }

            public Package Existing { get; set; }
            public bool TryUpdateSucceeds { get; set; } = true;

            public int TryUpdateCalls { get; private set; }
            public IUnitOfWork LastUnitOfWork { get; private set; }
            public PackageStatus LastNewStatus { get; private set; }
            public int? LastStorageLocationId { get; private set; }
            public int? LastCollectedByUserId { get; private set; }
            public byte[] LastRowVersion { get; private set; }
            public string LastLookup { get; private set; }

            public Package GetByF20Identifier(string f20Identifier) { LastLookup = f20Identifier; return Existing; }
            public Package GetById(int packageId) { return Existing; }
            public int Insert(Package package, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null) { throw new NotSupportedException("The service must use TryUpdateStatus."); }
            public PagedResult<Package> Search(PackageSearchCriteria criteria) { throw new NotSupportedException(); }

            public bool TryUpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, int? collectedByUserId, byte[] expectedRowVersion, IUnitOfWork unitOfWork = null)
            {
                TryUpdateCalls++;
                LastUnitOfWork = unitOfWork;
                LastNewStatus = newStatus;
                LastStorageLocationId = storageLocationId;
                LastCollectedByUserId = collectedByUserId;
                LastRowVersion = expectedRowVersion;
                _events.Add("update");
                return TryUpdateSucceeds;
            }
        }

        private sealed class FakeHistoryRepository : IPackageStatusHistoryRepository
        {
            private readonly List<string> _events;
            public FakeHistoryRepository(List<string> events) { _events = events; }

            public List<PackageStatusHistoryEntry> Entries { get; } = new List<PackageStatusHistoryEntry>();
            public List<IUnitOfWork> UnitsOfWork { get; } = new List<IUnitOfWork>();
            public Exception ThrowOnInsert { get; set; }

            public void Insert(PackageStatusHistoryEntry entry, IUnitOfWork unitOfWork = null)
            {
                _events.Add("history");
                if (ThrowOnInsert != null) throw ThrowOnInsert;
                Entries.Add(entry);
                UnitsOfWork.Add(unitOfWork);
            }

            public IEnumerable<PackageStatusHistoryEntry> GetByPackageId(int packageId) { return Entries; }
        }

        private sealed class FakeAuditLogRepository : IAuditLogRepository
        {
            private readonly List<string> _events;
            public FakeAuditLogRepository(List<string> events) { _events = events; }

            public List<AuditLogEntry> Entries { get; } = new List<AuditLogEntry>();
            public List<IUnitOfWork> UnitsOfWork { get; } = new List<IUnitOfWork>();
            public Exception ThrowOnInsert { get; set; }

            public void Insert(AuditLogEntry entry, IUnitOfWork unitOfWork = null)
            {
                _events.Add("audit");
                if (ThrowOnInsert != null) throw ThrowOnInsert;
                Entries.Add(entry);
                UnitsOfWork.Add(unitOfWork);
            }

            public IEnumerable<AuditLogEntry> GetRecent(int take) { return Entries.Take(take); }
        }

        private sealed class FakeListener : IPackageStatusChangeListener
        {
            private readonly List<string> _events;
            public FakeListener(List<string> events) { _events = events; }

            public List<PackageStatusChange> Changes { get; } = new List<PackageStatusChange>();
            public List<IUnitOfWork> UnitsOfWork { get; } = new List<IUnitOfWork>();
            public Exception Throw { get; set; }

            public void OnStatusChanged(PackageStatusChange change, IUnitOfWork unitOfWork)
            {
                _events.Add("listener");
                if (Throw != null) throw Throw;
                Changes.Add(change);
                UnitsOfWork.Add(unitOfWork);
            }
        }

        // ---------------------------------------------------------------------------------

        private const int UserId = 42;
        private static readonly byte[] RowVersion = { 0, 0, 0, 0, 0, 0, 0x07, 0xD1 };

        private List<string> _events;
        private FakePackageRepository _packages;
        private FakeHistoryRepository _history;
        private FakeAuditLogRepository _auditRepository;
        private FakeUnitOfWorkFactory _unitOfWorkFactory;
        private FakeListener _listener;
        private PackageStatusService _service;

        [TestInitialize]
        public void Setup()
        {
            _events = new List<string>();
            _packages = new FakePackageRepository(_events);
            _history = new FakeHistoryRepository(_events);
            _auditRepository = new FakeAuditLogRepository(_events);
            _unitOfWorkFactory = new FakeUnitOfWorkFactory(_events);
            _listener = new FakeListener(_events);
            _service = NewService(_listener);

            _packages.Existing = APackage(PackageStatus.Registered);
        }

        private PackageStatusService NewService(params IPackageStatusChangeListener[] listeners)
        {
            return new PackageStatusService(
                _packages, _history, new AuditLogger(_auditRepository), _unitOfWorkFactory, listeners);
        }

        private static Package APackage(PackageStatus status)
        {
            return new Package
            {
                PackageId = 10,
                F20Identifier = "F20-0004",
                Status = status,
                RowVersion = RowVersion,
                Recipient = new Recipient { FullName = "Pieter Khumalo", Email = "pieter@example.test" }
            };
        }

        private static IEnumerable<PackageStatus> AllStatuses()
        {
            return Enum.GetValues(typeof(PackageStatus)).Cast<PackageStatus>();
        }

        // ---- the happy path -----------------------------------------------------------------

        [TestMethod]
        [DataRow(PackageStatus.Registered, PackageStatus.InStorage)]
        [DataRow(PackageStatus.InStorage, PackageStatus.ReadyForCollection)]
        [DataRow(PackageStatus.ReadyForCollection, PackageStatus.Collected)]
        public void AllowedTransition_Succeeds_AndWritesHistoryAndAudit(PackageStatus from, PackageStatus to)
        {
            _packages.Existing = APackage(from);

            var result = _service.ChangeStatus("F20-0004", to, null, UserId, "scanned at the desk");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(StatusChangeOutcome.Changed, result.Outcome);
            Assert.IsNull(result.ErrorCode);
            Assert.AreEqual(from, result.FromStatus);
            Assert.AreEqual(to, result.ToStatus);

            Assert.AreEqual(to, _packages.LastNewStatus);

            var history = _history.Entries.Single();
            Assert.AreEqual(10, history.PackageId);
            Assert.AreEqual(from, history.FromStatus);
            Assert.AreEqual(to, history.ToStatus);
            Assert.AreEqual(UserId, history.ChangedByUserId);
            Assert.AreEqual("scanned at the desk", history.Notes);

            var audit = _auditRepository.Entries.Single();
            Assert.AreEqual(UserId, audit.UserId);
            Assert.AreEqual(AuditEntityTypes.Package, audit.EntityType);
            Assert.AreEqual("F20-0004", audit.EntityId);
        }

        [TestMethod]
        public void Success_CommitsOnce_InTheRightOrder()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            CollectionAssert.AreEqual(
                new[] { "update", "history", "audit", "listener", "commit" },
                _events);
            Assert.AreEqual(1, _unitOfWorkFactory.Started.Count);
            Assert.IsTrue(_unitOfWorkFactory.Started[0].Committed);
            Assert.IsTrue(_unitOfWorkFactory.Started[0].Disposed);
        }

        // ---- DR-009: rejected in the service layer, with nothing written ----------------------

        [TestMethod]
        public void EveryDisallowedPair_IsRejected_AndNothingIsWrittenOrStarted()
        {
            var rejected = 0;

            foreach (var from in AllStatuses())
            {
                foreach (var to in AllStatuses())
                {
                    if (PackageStatusTransitions.IsAllowed(from, to))
                    {
                        continue;
                    }

                    SetupAgain();
                    _packages.Existing = APackage(from);

                    var result = _service.ChangeStatus("F20-0004", to, null, UserId);

                    var pair = from + " -> " + to;
                    Assert.IsFalse(result.Success, pair);
                    Assert.AreEqual(StatusChangeOutcome.InvalidTransition, result.Outcome, pair);
                    Assert.AreEqual("InvalidTransition", result.ErrorCode, pair);
                    Assert.AreEqual(PackageStatusTransitions.RejectionMessage(from, to), result.Message, pair);
                    Assert.AreEqual(0, _packages.TryUpdateCalls, pair);
                    Assert.AreEqual(0, _history.Entries.Count, pair);
                    Assert.AreEqual(0, _auditRepository.Entries.Count, pair);
                    Assert.AreEqual(0, _listener.Changes.Count, pair);
                    Assert.AreEqual(0, _unitOfWorkFactory.Started.Count, pair + " (no transaction should even start)");
                    rejected++;
                }
            }

            Assert.AreEqual(13, rejected, "4 statuses = 16 pairs, 3 allowed, so 13 should have been rejected.");
        }

        [TestMethod]
        public void Rejection_ExplainsWhatIsAllowedInstead()
        {
            _packages.Existing = APackage(PackageStatus.Registered);

            var result = _service.ChangeStatus("F20-0004", PackageStatus.Collected, null, UserId);

            Assert.AreEqual("Package cannot move to Collected from Registered.", result.Message);
            CollectionAssert.AreEqual(new[] { PackageStatus.InStorage }, result.AllowedNextStatuses.ToArray());
        }

        [TestMethod]
        public void CollectedPackage_CannotBeMovedAnywhere()
        {
            _packages.Existing = APackage(PackageStatus.Collected);

            foreach (var to in AllStatuses())
            {
                var result = _service.ChangeStatus("F20-0004", to, null, UserId);
                Assert.AreEqual(StatusChangeOutcome.InvalidTransition, result.Outcome, "Collected -> " + to);
                Assert.AreEqual(0, result.AllowedNextStatuses.Count);
            }
        }

        // ---- not found -------------------------------------------------------------------------

        [TestMethod]
        public void UnknownPackage_ReturnsNotFound_AndWritesNothing()
        {
            _packages.Existing = null;

            var result = _service.ChangeStatus("NOPE-0000", PackageStatus.InStorage, null, UserId);

            Assert.AreEqual(StatusChangeOutcome.NotFound, result.Outcome);
            Assert.AreEqual("NotFound", result.ErrorCode);
            Assert.IsNull(result.FromStatus);
            Assert.AreEqual(0, _unitOfWorkFactory.Started.Count);
            Assert.AreEqual(0, _history.Entries.Count);
        }

        // ---- NFR-023: concurrent edits -------------------------------------------------------

        [TestMethod]
        public void TheRowVersionThatWasReadIsWhatGetsChecked()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            CollectionAssert.AreEqual(RowVersion, _packages.LastRowVersion);
        }

        [TestMethod]
        public void SomeoneElseChangedItFirst_ReturnsConflict_AndNothingElseIsWritten()
        {
            _packages.TryUpdateSucceeds = false;

            var result = _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            Assert.AreEqual(StatusChangeOutcome.ConcurrentUpdate, result.Outcome);
            Assert.AreEqual("ConcurrentUpdate", result.ErrorCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Message));
            Assert.AreEqual(0, _history.Entries.Count, "The loser must not leave a history row.");
            Assert.AreEqual(0, _auditRepository.Entries.Count, "The loser must not leave an audit entry.");
            Assert.AreEqual(0, _listener.Changes.Count, "Nobody should be notified of a change that didn't happen.");
            Assert.IsFalse(_unitOfWorkFactory.Started.Single().Committed);
            Assert.IsTrue(_unitOfWorkFactory.Started.Single().Disposed);
        }

        // ---- DR-010: atomic ------------------------------------------------------------------

        [TestMethod]
        public void EveryWrite_UsesTheSameUnitOfWork()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            var uow = _unitOfWorkFactory.Started.Single();
            Assert.AreSame(uow, _packages.LastUnitOfWork);
            Assert.AreSame(uow, _history.UnitsOfWork.Single());
            Assert.AreSame(uow, _auditRepository.UnitsOfWork.Single());
            Assert.AreSame(uow, _listener.UnitsOfWork.Single());
        }

        [TestMethod]
        public void HistoryInsertFails_NothingIsCommitted_AndTheErrorIsNotSwallowed()
        {
            _history.ThrowOnInsert = new InvalidOperationException("boom");

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId));

            Assert.AreEqual("boom", ex.Message);
            Assert.IsFalse(_unitOfWorkFactory.Started.Single().Committed);
            Assert.IsTrue(_unitOfWorkFactory.Started.Single().Disposed, "Disposing without a commit is what rolls it back.");
            Assert.AreEqual(0, _auditRepository.Entries.Count);
        }

        [TestMethod]
        public void AuditInsertFails_NothingIsCommitted()
        {
            _auditRepository.ThrowOnInsert = new InvalidOperationException("audit down");

            Assert.ThrowsExactly<InvalidOperationException>(
                () => _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId));

            Assert.IsFalse(_unitOfWorkFactory.Started.Single().Committed);
            CollectionAssert.DoesNotContain(_events, "commit");
        }

        [TestMethod]
        public void ListenerFails_NothingIsCommitted()
        {
            _listener.Throw = new InvalidOperationException("queue is full");

            Assert.ThrowsExactly<InvalidOperationException>(
                () => _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId));

            Assert.IsFalse(_unitOfWorkFactory.Started.Single().Committed);
            CollectionAssert.DoesNotContain(_events, "commit");
        }

        // ---- the notification hook ---------------------------------------------------------

        [TestMethod]
        public void Listener_IsTold_WhatChanged()
        {
            var before = DateTime.UtcNow;

            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            var change = _listener.Changes.Single();
            Assert.AreEqual(10, change.PackageId);
            Assert.AreEqual("F20-0004", change.F20Identifier);
            Assert.AreEqual(PackageStatus.Registered, change.FromStatus);
            Assert.AreEqual(PackageStatus.InStorage, change.ToStatus);
            Assert.AreEqual(UserId, change.ChangedByUserId);
            Assert.IsTrue(change.ChangedAtUtc >= before && change.ChangedAtUtc <= DateTime.UtcNow);
        }

        [TestMethod]
        public void EveryListener_IsCalled_InOrder()
        {
            var second = new FakeListener(_events);
            var service = NewService(_listener, second);

            service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            Assert.AreEqual(1, _listener.Changes.Count);
            Assert.AreEqual(1, second.Changes.Count);
            CollectionAssert.AreEqual(
                new[] { "update", "history", "audit", "listener", "listener", "commit" },
                _events);
        }

        [TestMethod]
        public void NoListeners_IsFine()
        {
            var service = NewService();

            var result = service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            Assert.IsTrue(result.Success);
        }

        // ---- collection, location, notes, audit content --------------------------------------

        [TestMethod]
        public void Collecting_RecordsWhoCollected_AndAuditsItAsACollection()
        {
            _packages.Existing = APackage(PackageStatus.ReadyForCollection);

            _service.ChangeStatus("F20-0004", PackageStatus.Collected, null, UserId);

            Assert.AreEqual(UserId, _packages.LastCollectedByUserId);
            Assert.AreEqual(AuditActions.PackageCollected, _auditRepository.Entries.Single().Action);
        }

        [TestMethod]
        public void OtherChanges_DoNotSetCollectedBy_AndAreAuditedAsStatusChanges()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            Assert.IsNull(_packages.LastCollectedByUserId);
            Assert.AreEqual(AuditActions.PackageStatusChanged, _auditRepository.Entries.Single().Action);
        }

        [TestMethod]
        public void StorageLocation_IsPassedThrough()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, 5, UserId);

            Assert.AreEqual(5, _packages.LastStorageLocationId);
        }

        [TestMethod]
        public void NoStorageLocation_IsPassedAsNull_SoTheExistingOneIsKept()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            Assert.IsNull(_packages.LastStorageLocationId);
        }

        [TestMethod]
        public void AuditDetail_HasOnlyStatuses_NoPersonalData()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            var detail = _auditRepository.Entries.Single().Detail;
            Assert.AreEqual("Registered -> In Storage", detail);
            StringAssert.DoesNotMatch(detail, new System.Text.RegularExpressions.Regex("Khumalo|Pieter|example"));
        }

        [TestMethod]
        public void OverlongNotes_AreCut_InsteadOfFailingTheChange()
        {
            var result = _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId, new string('x', 1000));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PackageStatusService.MaxNotesLength, _history.Entries.Single().Notes.Length);
        }

        [TestMethod]
        public void NoNotes_StaysNull()
        {
            _service.ChangeStatus("F20-0004", PackageStatus.InStorage, null, UserId);

            Assert.IsNull(_history.Entries.Single().Notes);
        }

        [TestMethod]
        public void TheIdentifier_IsTrimmedBeforeLookup()
        {
            _service.ChangeStatus("  F20-0004 \t", PackageStatus.InStorage, null, UserId);

            Assert.AreEqual("F20-0004", _packages.LastLookup);
        }

        // ---- bad input is a programming error, not a business outcome ------------------------

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void BlankIdentifier_Throws(string identifier)
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => _service.ChangeStatus(identifier, PackageStatus.InStorage, null, UserId));
        }

        [TestMethod]
        public void UnknownStatusValue_Throws()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => _service.ChangeStatus("F20-0004", (PackageStatus)99, null, UserId));
            Assert.AreEqual(0, _unitOfWorkFactory.Started.Count);
        }

        [TestMethod]
        public void Constructor_NullDependencies_Throw()
        {
            var audit = new AuditLogger(_auditRepository);

            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageStatusService(null, _history, audit, _unitOfWorkFactory));
            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageStatusService(_packages, null, audit, _unitOfWorkFactory));
            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageStatusService(_packages, _history, null, _unitOfWorkFactory));
            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageStatusService(_packages, _history, audit, null));
        }

        // Starts every case from a clean slate when a test loops over many pairs
        private void SetupAgain()
        {
            Setup();
        }
    }
}
