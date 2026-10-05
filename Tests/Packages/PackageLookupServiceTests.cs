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
    public class PackageLookupServiceTests
    {
        private sealed class FakePackageRepository : IPackageRepository
        {
            public List<string> Lookups { get; } = new List<string>();
            public Package Result { get; set; }

            public Package GetByF20Identifier(string f20Identifier) { Lookups.Add(f20Identifier); return Result; }
            public Package GetById(int packageId) { throw new NotSupportedException(); }
            public int Insert(Package package, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public bool TryUpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, int? collectedByUserId, byte[] expectedRowVersion, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public PagedResult<Package> Search(PackageSearchCriteria criteria) { throw new NotSupportedException(); }
        }

        private FakePackageRepository _repository;
        private PackageLookupService _service;

        [TestInitialize]
        public void Setup()
        {
            _repository = new FakePackageRepository();
            _service = new PackageLookupService(_repository);
        }

        [TestMethod]
        public void KnownCode_ReturnsThePackage()
        {
            var package = new Package { PackageId = 7, F20Identifier = "F20-0001" };
            _repository.Result = package;

            Assert.AreSame(package, _service.Find("F20-0001"));
        }

        [TestMethod]
        public void UnknownCode_ReturnsNull()
        {
            _repository.Result = null;

            Assert.IsNull(_service.Find("F20-9999"));
            Assert.AreEqual(1, _repository.Lookups.Count);
        }

        [TestMethod]
        public void ScannerNoise_IsTrimmed_BeforeTheLookup()
        {
            _service.Find("F20-0001\r\n");

            Assert.AreEqual("F20-0001", _repository.Lookups[0]);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("F20-0001'; DROP TABLE Packages;--")]
        [DataRow("not a package code")]
        [DataRow("1234567890123456789012345678901")]
        public void MalformedCode_ReturnsNull_WithoutTouchingTheDatabase(string scanned)
        {
            _repository.Result = new Package { F20Identifier = "SHOULD-NOT-BE-RETURNED" };

            Assert.IsNull(_service.Find(scanned));
            Assert.AreEqual(0, _repository.Lookups.Count);
        }

        [TestMethod]
        public void Constructor_NullRepository_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PackageLookupService(null));
        }
    }
}
