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
    public class PackageRegistrationServiceTests
    {
        private sealed class FakePackages : IPackageRepository
        {
            public List<Package> Inserted { get; } = new List<Package>();

            public int Insert(Package package, IUnitOfWork unitOfWork = null)
            {
                package.PackageId = 500 + Inserted.Count;
                Inserted.Add(package);
                return package.PackageId;
            }

            public Package GetByF20Identifier(string f20Identifier) { throw new NotSupportedException(); }
            public Package GetById(int packageId) { throw new NotSupportedException(); }
            public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public bool TryUpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, int? collectedByUserId, byte[] expectedRowVersion, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
            public PagedResult<Package> Search(PackageSearchCriteria criteria) { throw new NotSupportedException(); }
        }

        private sealed class FakeRegistration : IPackageRegistrationRepository
        {
            public int NextNumber = 201;
            public List<Recipient> Recipients { get; } = new List<Recipient>();

            public int InsertRecipient(Recipient recipient, IUnitOfWork unitOfWork)
            {
                Assert.IsNotNull(unitOfWork, "The recipient must be saved inside the registration's transaction.");
                Recipients.Add(recipient);
                return 900 + Recipients.Count;
            }

            public int NextF20Number(IUnitOfWork unitOfWork)
            {
                Assert.IsNotNull(unitOfWork, "Numbering must happen inside the transaction, or two clerks could get the same number.");
                return NextNumber;
            }
        }

        private sealed class FakeHistory : IPackageStatusHistoryRepository
        {
            public List<PackageStatusHistoryEntry> Entries { get; } = new List<PackageStatusHistoryEntry>();
            public void Insert(PackageStatusHistoryEntry entry, IUnitOfWork unitOfWork = null) { Entries.Add(entry); }
            public IEnumerable<PackageStatusHistoryEntry> GetByPackageId(int packageId) { return Entries.Where(e => e.PackageId == packageId); }
        }

        private sealed class FakeLocations : IStorageLocationRepository
        {
            public IEnumerable<StorageLocation> GetAll(bool activeOnly = true) { throw new NotSupportedException(); }

            public StorageLocation GetById(int storageLocationId)
            {
                if (storageLocationId == 3) return new StorageLocation { StorageLocationId = 3, Code = "Shelf A-3", IsActive = true };
                if (storageLocationId == 9) return new StorageLocation { StorageLocationId = 9, Code = "Old shelf", IsActive = false };
                return null;
            }

            public int Insert(StorageLocation location, IUnitOfWork unitOfWork = null) { throw new NotSupportedException(); }
        }

        private sealed class FakeConfig : IAppConfigRepository
        {
            public Dictionary<string, string> Values { get; } = new Dictionary<string, string>
            {
                { "Fee.Personal", "10.00" },
                { "Fee.WorkRelated", "0.00" }
            };

            public string GetValue(string key)
            {
                string value;
                return Values.TryGetValue(key, out value) ? value : null;
            }
        }

        private sealed class AuditCall
        {
            public string Action, EntityType, EntityId, Detail;
            public int? UserId;
        }

        private sealed class FakeAudit : IAuditLogger
        {
            public List<AuditCall> Calls { get; } = new List<AuditCall>();

            public void Log(string action, string entityType, string entityId, string detail, int? userId = null, IUnitOfWork unitOfWork = null)
            {
                Calls.Add(new AuditCall { Action = action, EntityType = entityType, EntityId = entityId, Detail = detail, UserId = userId });
            }
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public bool Committed { get; private set; }
            public IDbConnection Connection => null;
            public IDbTransaction Transaction => null;
            public void Commit() { Committed = true; }
            public void Dispose() { }
        }

        private sealed class FakeUnitOfWorkFactory : IUnitOfWorkFactory
        {
            public List<FakeUnitOfWork> Started { get; } = new List<FakeUnitOfWork>();

            public IUnitOfWork Begin()
            {
                var unitOfWork = new FakeUnitOfWork();
                Started.Add(unitOfWork);
                return unitOfWork;
            }
        }

        private const int ClerkId = 2;

        private FakePackages _packages;
        private FakeRegistration _registration;
        private FakeHistory _history;
        private FakeConfig _config;
        private FakeAudit _audit;
        private FakeUnitOfWorkFactory _unitOfWork;
        private PackageRegistrationService _service;

        [TestInitialize]
        public void SetUp()
        {
            _packages = new FakePackages();
            _registration = new FakeRegistration();
            _history = new FakeHistory();
            _config = new FakeConfig();
            _audit = new FakeAudit();
            _unitOfWork = new FakeUnitOfWorkFactory();
            _service = new PackageRegistrationService(_packages, _registration, _history, new FakeLocations(), _config, _audit, _unitOfWork);
        }

        private static PackageRegistrationRequest ValidRequest()
        {
            return new PackageRegistrationRequest
            {
                RecipientFullName = "Thabo Nkosi",
                RecipientIdentifierNo = "12345678",
                RecipientEmail = "thabo.nkosi@courier.test",
                RecipientPhone = "082 123 4567",
                RecipientDepartment = "Computer Science",
                SenderName = "Takealot",
                PackageType = "Box",
                Classification = "Personal",
                StorageLocationId = 3,
                Notes = "Fragile"
            };
        }

        // ---- Success ----

        [TestMethod]
        public void ValidPersonalPackage_IsRegisteredWithTheNextF20Identifier_AndTheConfiguredFee()
        {
            var result = _service.Register(ValidRequest(), ClerkId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("F20-0201", result.F20Identifier);
            Assert.AreEqual(500, result.PackageId);
            Assert.AreEqual(10.00m, result.Fee);
            Assert.AreEqual("Unpaid", result.PaymentStatus);

            var saved = _packages.Inserted.Single();
            Assert.AreEqual("F20-0201", saved.F20Identifier);
            Assert.AreEqual(PackageStatus.Registered, saved.Status);
            Assert.AreEqual("Personal", saved.Classification);
            Assert.AreEqual("Box", saved.PackageType);
            Assert.AreEqual(3, saved.StorageLocationId);
            Assert.AreEqual(901, saved.RecipientId);
            Assert.AreEqual(ClerkId, saved.CreatedByUserId);
            Assert.AreEqual("Takealot", saved.SenderName);
            Assert.AreEqual("Fragile", saved.Notes);
        }

        [TestMethod]
        public void WorkRelatedPackage_IsFree_AndExempt()
        {
            var request = ValidRequest();
            request.Classification = "WorkRelated";

            var result = _service.Register(request, ClerkId);

            Assert.AreEqual(0.00m, result.Fee);
            Assert.AreEqual("Exempt", result.PaymentStatus);
            Assert.AreEqual("Exempt", _packages.Inserted.Single().PaymentStatus);
        }

        [TestMethod]
        public void TheFeeComesFromConfig_SoAChangedFeeIsUsedStraightAway()
        {
            _config.Values["Fee.Personal"] = "12.50";

            Assert.AreEqual(12.50m, _service.Register(ValidRequest(), ClerkId).Fee);
        }

        [TestMethod]
        public void ClassificationAcceptsTheUsualSpellings()
        {
            foreach (var spelling in new[] { "workrelated", "Work-related", "work related", "WORKRELATED" })
            {
                var request = ValidRequest();
                request.Classification = spelling;
                Assert.AreEqual("WorkRelated", Register(request).Classification, spelling);
            }
        }

        private Package Register(PackageRegistrationRequest request)
        {
            var before = _packages.Inserted.Count;
            Assert.IsTrue(_service.Register(request, ClerkId).Success);
            return _packages.Inserted[before];
        }

        [TestMethod]
        public void RecipientIsSavedTrimmed_WithThePhoneNumberDigitsOnly()
        {
            var request = ValidRequest();
            request.RecipientFullName = "  Thabo Nkosi  ";
            request.RecipientPhone = "(082) 123-4567";

            _service.Register(request, ClerkId);

            var recipient = _registration.Recipients.Single();
            Assert.AreEqual("Thabo Nkosi", recipient.FullName);
            Assert.AreEqual("0821234567", recipient.PhoneNumber);
            Assert.AreEqual("12345678", recipient.IdentifierNo);
            Assert.AreEqual("Computer Science", recipient.Department);
        }

        [TestMethod]
        public void HistoryStartsAtRegistered_AndAuditHasNoPersonalDetails_AllInOneTransaction()
        {
            _service.Register(ValidRequest(), ClerkId);

            var history = _history.Entries.Single();
            Assert.IsNull(history.FromStatus);
            Assert.AreEqual(PackageStatus.Registered, history.ToStatus);
            Assert.AreEqual(500, history.PackageId);
            Assert.AreEqual(ClerkId, history.ChangedByUserId);

            var audit = _audit.Calls.Single();
            Assert.AreEqual(AuditActions.PackageCreated, audit.Action);
            Assert.AreEqual(AuditEntityTypes.Package, audit.EntityType);
            Assert.AreEqual("F20-0201", audit.EntityId);
            Assert.AreEqual("Personal, fee 10.00", audit.Detail);
            Assert.AreEqual(ClerkId, audit.UserId);
            Assert.IsFalse(audit.Detail.Contains("Thabo") || audit.Detail.Contains("@"), "No names or emails in the audit log (SR-03)");

            Assert.AreEqual(1, _unitOfWork.Started.Count);
            Assert.IsTrue(_unitOfWork.Started[0].Committed);
        }

        [TestMethod]
        public void OptionalFieldsCanBeLeftOut()
        {
            var request = ValidRequest();
            request.RecipientDepartment = "   ";
            request.Notes = null;

            Assert.IsTrue(_service.Register(request, ClerkId).Success);
            Assert.IsNull(_registration.Recipients.Single().Department);
            Assert.IsNull(_packages.Inserted.Single().Notes);
        }

        // ---- Identifier format ----

        [TestMethod]
        public void F20Identifiers_HaveAtLeastFourDigits()
        {
            Assert.AreEqual("F20-0001", PackageRegistrationService.FormatF20Identifier(1));
            Assert.AreEqual("F20-0201", PackageRegistrationService.FormatF20Identifier(201));
            Assert.AreEqual("F20-9999", PackageRegistrationService.FormatF20Identifier(9999));
            Assert.AreEqual("F20-60001", PackageRegistrationService.FormatF20Identifier(60001));
        }

        [TestMethod]
        public void NewIdentifiers_PassTheScanScreensIdentifierCheck()
        {
            string normalized;
            Assert.IsTrue(PackageIdentifier.TryNormalize(PackageRegistrationService.FormatF20Identifier(60001), out normalized));
        }

        // ---- Refusals: nothing is written ----

        private void AssertRefused(PackageRegistrationRequest request, string messagePart)
        {
            var result = _service.Register(request, ClerkId);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("ValidationError", result.ErrorCode);
            Assert.IsTrue(result.Message.Contains(messagePart), "Message was: " + result.Message);
            Assert.AreEqual(0, _packages.Inserted.Count);
            Assert.AreEqual(0, _registration.Recipients.Count);
            Assert.AreEqual(0, _audit.Calls.Count);
            Assert.AreEqual(0, _unitOfWork.Started.Count, "Nothing is started before the input is valid");
        }

        [TestMethod]
        public void MissingRequiredFields_AreRefused()
        {
            var r = ValidRequest(); r.RecipientFullName = " "; AssertRefused(r, "full name");
            r = ValidRequest(); r.RecipientIdentifierNo = null; AssertRefused(r, "student number");
            r = ValidRequest(); r.RecipientEmail = ""; AssertRefused(r, "email");
            r = ValidRequest(); r.RecipientPhone = null; AssertRefused(r, "phone");
            r = ValidRequest(); r.SenderName = null; AssertRefused(r, "Sender");
            r = ValidRequest(); r.StorageLocationId = null; AssertRefused(r, "Storage location");
        }

        [TestMethod]
        public void NullRequest_IsRefused()
        {
            Assert.IsFalse(_service.Register(null, ClerkId).Success);
        }

        [TestMethod]
        public void BadEmailOrPhone_IsRefused()
        {
            var r = ValidRequest(); r.RecipientEmail = "not-an-email"; AssertRefused(r, "email");
            r = ValidRequest(); r.RecipientPhone = "1234"; AssertRefused(r, "phone");
            r = ValidRequest(); r.RecipientPhone = "082-ABC-4567"; AssertRefused(r, "phone");
        }

        [TestMethod]
        public void UnknownPackageTypeOrClassification_IsRefused()
        {
            var r = ValidRequest(); r.PackageType = "Crate"; AssertRefused(r, "Package type");
            r = ValidRequest(); r.Classification = "Business"; AssertRefused(r, "Classification");
            r = ValidRequest(); r.Classification = null; AssertRefused(r, "Classification");
        }

        [TestMethod]
        public void UnknownOrRetiredStorageLocation_IsRefused()
        {
            var r = ValidRequest(); r.StorageLocationId = 404; AssertRefused(r, "storage location");
            r = ValidRequest(); r.StorageLocationId = 9; AssertRefused(r, "storage location");
        }

        [TestMethod]
        public void OverlongFields_AreRefused_InsteadOfFailingInTheDatabase()
        {
            var r = ValidRequest(); r.RecipientFullName = new string('a', 151); AssertRefused(r, "at most 150");
            r = ValidRequest(); r.Notes = new string('n', 501); AssertRefused(r, "at most 500");
            r = ValidRequest(); r.SenderName = new string('s', 151); AssertRefused(r, "at most 150");
        }

        [TestMethod]
        public void MissingFeeConfig_IsASetupError_NotTheClerksFault()
        {
            _config.Values.Remove("Fee.Personal");

            Assert.ThrowsExactly<InvalidOperationException>(() => _service.Register(ValidRequest(), ClerkId));
            Assert.AreEqual(0, _packages.Inserted.Count);
        }
    }
}