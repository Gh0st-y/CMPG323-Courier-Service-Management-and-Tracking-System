using System;
using System.Collections.Generic;
using System.Linq;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.StorageLocations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.StorageLocations
{
    [TestClass]
    public class StorageLocationAdminServiceTests
    {
        private sealed class FakeLocations : IStorageLocationRepository
        {
            public readonly List<StorageLocation> Rows = new List<StorageLocation>();
            public readonly List<IUnitOfWork> InsertUnitsOfWork = new List<IUnitOfWork>();

            public IEnumerable<StorageLocation> GetAll(bool activeOnly = true)
            {
                return Rows.Where(l => !activeOnly || l.IsActive).ToList();
            }

            public StorageLocation GetById(int storageLocationId) => Rows.FirstOrDefault(l => l.StorageLocationId == storageLocationId);

            public int Insert(StorageLocation location, IUnitOfWork unitOfWork = null)
            {
                InsertUnitsOfWork.Add(unitOfWork);
                var id = Rows.Count == 0 ? 1 : Rows.Max(l => l.StorageLocationId) + 1;
                Rows.Add(new StorageLocation { StorageLocationId = id, Code = location.Code, Description = location.Description, IsActive = location.IsActive });
                return id;
            }
        }

        private sealed class FakeAuditLogger : IAuditLogger
        {
            public readonly List<Tuple<string, string, string, string, int?, IUnitOfWork>> Entries =
                new List<Tuple<string, string, string, string, int?, IUnitOfWork>>();

            public void Log(string action, string entityType, string entityId, string detail, int? userId = null, IUnitOfWork unitOfWork = null)
            {
                Entries.Add(Tuple.Create(action, entityType, entityId, detail, userId, unitOfWork));
            }
        }

        private sealed class TrackingUnitOfWork : IUnitOfWork
        {
            public bool Committed;
            public System.Data.IDbConnection Connection => null;
            public System.Data.IDbTransaction Transaction => null;
            public void Commit() { Committed = true; }
            public void Dispose() { }
        }

        private sealed class TrackingUnitOfWorkFactory : IUnitOfWorkFactory
        {
            public readonly List<TrackingUnitOfWork> Started = new List<TrackingUnitOfWork>();
            public IUnitOfWork Begin() { var u = new TrackingUnitOfWork(); Started.Add(u); return u; }
        }

        private FakeLocations _locations;
        private FakeAuditLogger _audit;
        private TrackingUnitOfWorkFactory _unitsOfWork;
        private StorageLocationAdminService _service;

        [TestInitialize]
        public void SetUp()
        {
            _locations = new FakeLocations();
            _locations.Rows.Add(new StorageLocation { StorageLocationId = 1, Code = "Shelf A-1", IsActive = true });
            _locations.Rows.Add(new StorageLocation { StorageLocationId = 2, Code = "Old Store Room", IsActive = false });
            _audit = new FakeAuditLogger();
            _unitsOfWork = new TrackingUnitOfWorkFactory();
            _service = new StorageLocationAdminService(_locations, _audit, _unitsOfWork);
        }

        [TestMethod]
        public void Create_SavesActiveLocation_AndAuditsInTheSameTransaction()
        {
            var result = _service.Create("  Shelf D-1 ", "  Bottom shelf, left  ", 5);

            Assert.AreEqual(CreateLocationOutcome.Created, result.Outcome);
            Assert.AreEqual(3, result.Location.StorageLocationId);
            Assert.AreEqual("Shelf D-1", result.Location.Code);
            Assert.AreEqual("Bottom shelf, left", result.Location.Description);

            var saved = _locations.GetById(3);
            Assert.AreEqual("Shelf D-1", saved.Code);
            Assert.IsTrue(saved.IsActive);

            Assert.AreEqual(1, _unitsOfWork.Started.Count);
            var unitOfWork = _unitsOfWork.Started[0];
            Assert.IsTrue(unitOfWork.Committed);
            Assert.AreSame(unitOfWork, _locations.InsertUnitsOfWork.Single());

            var entry = _audit.Entries.Single();
            Assert.AreEqual(AuditActions.StorageLocationCreated, entry.Item1);
            Assert.AreEqual(AuditEntityTypes.StorageLocation, entry.Item2);
            Assert.AreEqual("3", entry.Item3);
            Assert.AreEqual("Storage location Shelf D-1 added", entry.Item4);
            Assert.AreEqual(5, entry.Item5);
            Assert.AreSame(unitOfWork, entry.Item6);
        }

        [TestMethod]
        public void Create_BlankDescription_IsSavedAsNull()
        {
            var result = _service.Create("Shelf D-2", "   ", 5);

            Assert.AreEqual(CreateLocationOutcome.Created, result.Outcome);
            Assert.IsNull(_locations.GetById(result.Location.StorageLocationId).Description);
        }

        [TestMethod]
        public void Create_MissingCode_IsInvalid_AndSavesNothing()
        {
            foreach (var code in new[] { null, "", "   " })
            {
                var result = _service.Create(code, "x", 5);
                Assert.AreEqual(CreateLocationOutcome.Invalid, result.Outcome);
                Assert.AreEqual("Code is required.", result.Message);
            }

            AssertNothingSaved();
        }

        [TestMethod]
        public void Create_CodeLength_50IsAllowed_51IsNot()
        {
            var tooLong = _service.Create(new string('C', 51), null, 5);
            Assert.AreEqual(CreateLocationOutcome.Invalid, tooLong.Outcome);
            Assert.AreEqual("Code must be 50 characters or fewer.", tooLong.Message);
            AssertNothingSaved();

            var longest = _service.Create(new string('C', 50), null, 5);
            Assert.AreEqual(CreateLocationOutcome.Created, longest.Outcome);
        }

        [TestMethod]
        public void Create_DescriptionLength_200IsAllowed_201IsNot()
        {
            var tooLong = _service.Create("Shelf D-3", new string('d', 201), 5);
            Assert.AreEqual(CreateLocationOutcome.Invalid, tooLong.Outcome);
            Assert.AreEqual("Description must be 200 characters or fewer.", tooLong.Message);
            AssertNothingSaved();

            var longest = _service.Create("Shelf D-3", new string('d', 200), 5);
            Assert.AreEqual(CreateLocationOutcome.Created, longest.Outcome);
        }

        [TestMethod]
        public void Create_ExistingCode_InAnyCase_IsDuplicate()
        {
            var result = _service.Create("shelf a-1", null, 5);

            Assert.AreEqual(CreateLocationOutcome.DuplicateCode, result.Outcome);
            Assert.AreEqual("A storage location with that code already exists.", result.Message);
            AssertNothingSaved();
        }

        [TestMethod]
        public void Create_CodeOfAnInactiveLocation_IsDuplicate()
        {
            var result = _service.Create("Old Store Room", null, 5);

            Assert.AreEqual(CreateLocationOutcome.DuplicateCode, result.Outcome);
            AssertNothingSaved();
        }

        private void AssertNothingSaved()
        {
            Assert.AreEqual(2, _locations.Rows.Count);
            Assert.AreEqual(0, _audit.Entries.Count);
            Assert.AreEqual(0, _unitsOfWork.Started.Count);
        }
    }
}
