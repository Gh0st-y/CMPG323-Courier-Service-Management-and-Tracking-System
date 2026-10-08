using System;
using System.Collections.Generic;
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
    public class PaymentStatusServiceTests
    {
        private sealed class FakePackageRepository : IPackageRepository
        {
            public readonly Dictionary<string, Package> ByIdentifier = new Dictionary<string, Package>();

            public Package GetByF20Identifier(string f20Identifier)
            {
                Package package;
                return ByIdentifier.TryGetValue(f20Identifier, out package) ? package : null;
            }

            public Package GetById(int packageId) => throw new NotSupportedException();
            public int Insert(Package package, IUnitOfWork unitOfWork = null) => throw new NotSupportedException();
            public void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null) => throw new NotSupportedException();
            public bool TryUpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, int? collectedByUserId, byte[] expectedRowVersion, IUnitOfWork unitOfWork = null) => throw new NotSupportedException();
            public PagedResult<Package> Search(PackageSearchCriteria criteria) => throw new NotSupportedException();
        }

        /// <summary>Stands in for dbo.Packages' payment columns.</summary>
        private sealed class FakePaymentRepository : IPackagePaymentRepository
        {
            public readonly Dictionary<int, PaymentInfo> Rows = new Dictionary<int, PaymentInfo>();
            public readonly List<IUnitOfWork> UpdateUnitsOfWork = new List<IUnitOfWork>();
            public int Updates;
            public DateTime Now = new DateTime(2026, 10, 9, 9, 30, 0, DateTimeKind.Utc);

            public PaymentInfo GetPaymentInfo(int packageId, IUnitOfWork unitOfWork = null)
            {
                PaymentInfo row;
                return Rows.TryGetValue(packageId, out row)
                    ? new PaymentInfo { PackageId = row.PackageId, PaymentStatus = row.PaymentStatus, UpdatedAtUtc = row.UpdatedAtUtc, UpdatedBy = row.UpdatedBy }
                    : null;
            }

            public bool UpdatePaymentStatus(int packageId, string paymentStatus, int changedByUserId, IUnitOfWork unitOfWork = null)
            {
                PaymentInfo row;
                if (!Rows.TryGetValue(packageId, out row))
                {
                    return false;
                }

                Updates++;
                UpdateUnitsOfWork.Add(unitOfWork);
                row.PaymentStatus = paymentStatus;
                row.UpdatedAtUtc = Now;
                row.UpdatedBy = "user" + changedByUserId;
                return true;
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

        private sealed class FakeAuditLogger : IAuditLogger
        {
            public readonly List<string[]> Entries = new List<string[]>();
            public readonly List<IUnitOfWork> UnitsOfWork = new List<IUnitOfWork>();

            public void Log(string action, string entityType, string entityId, string detail, int? userId = null, IUnitOfWork unitOfWork = null)
            {
                Entries.Add(new[] { action, entityType, entityId, detail, userId.ToString() });
                UnitsOfWork.Add(unitOfWork);
            }
        }

        private FakePackageRepository _packages;
        private FakePaymentRepository _payments;
        private FakeAuditLogger _audit;
        private TrackingUnitOfWorkFactory _unitsOfWork;
        private PaymentStatusService _service;

        [TestInitialize]
        public void TestInitialize()
        {
            _packages = new FakePackageRepository();
            _payments = new FakePaymentRepository();
            _audit = new FakeAuditLogger();
            _unitsOfWork = new TrackingUnitOfWorkFactory();
            _service = new PaymentStatusService(_packages, _payments, _audit, _unitsOfWork);

            AddPackage(7, "F20-0007", "Personal", 10.00m, PaymentStatuses.Unpaid);
        }

        private void AddPackage(int id, string f20, string classification, decimal fee, string paymentStatus)
        {
            _packages.ByIdentifier[f20] = new Package { PackageId = id, F20Identifier = f20, Classification = classification, Fee = fee, PaymentStatus = paymentStatus };
            _payments.Rows[id] = new PaymentInfo { PackageId = id, PaymentStatus = paymentStatus };
        }

        [TestMethod]
        public void Paid_IsStored_WithTimeAndStaffMember()
        {
            var result = _service.Change("F20-0007", PaymentStatuses.Paid, 4);

            Assert.AreEqual(PaymentChangeOutcome.Changed, result.Outcome);
            Assert.AreEqual("F20-0007", result.F20Identifier);
            Assert.AreEqual("Paid", result.Payment.PaymentStatus);
            Assert.AreEqual(_payments.Now, result.Payment.UpdatedAtUtc);
            Assert.AreEqual("user4", result.Payment.UpdatedBy);
            Assert.AreEqual("Paid", _payments.Rows[7].PaymentStatus);
        }

        [TestMethod]
        public void Change_IsAudited_InTheSameTransaction()
        {
            _service.Change("F20-0007", PaymentStatuses.Paid, 4);

            Assert.AreEqual(1, _audit.Entries.Count);
            var entry = _audit.Entries[0];
            Assert.AreEqual(AuditActions.PaymentStatusChanged, entry[0]);
            Assert.AreEqual(AuditEntityTypes.Package, entry[1]);
            Assert.AreEqual("F20-0007", entry[2]);
            Assert.AreEqual("Unpaid to Paid (fee R10.00)", entry[3]);
            Assert.AreEqual("4", entry[4]);

            Assert.AreEqual(1, _unitsOfWork.Started.Count);
            Assert.IsTrue(_unitsOfWork.Started[0].Committed);
            Assert.AreSame(_unitsOfWork.Started[0], _audit.UnitsOfWork[0]);
            Assert.AreSame(_unitsOfWork.Started[0], _payments.UpdateUnitsOfWork[0]);
        }

        [TestMethod]
        public void SameStatusAgain_WritesNothing()
        {
            var result = _service.Change("F20-0007", PaymentStatuses.Unpaid, 4);

            Assert.AreEqual(PaymentChangeOutcome.Unchanged, result.Outcome);
            Assert.AreEqual("Unpaid", result.Payment.PaymentStatus);
            Assert.AreEqual(0, _payments.Updates);
            Assert.AreEqual(0, _audit.Entries.Count);
            Assert.AreEqual(0, _unitsOfWork.Started.Count);
        }

        [TestMethod]
        public void AnyStatus_CanBeSetFromAnyOther()
        {
            AddPackage(8, "F20-0008", "WorkRelated", 0m, PaymentStatuses.Exempt);

            Assert.AreEqual(PaymentChangeOutcome.Changed, _service.Change("F20-0008", PaymentStatuses.Unpaid, 4).Outcome);
            Assert.AreEqual(PaymentChangeOutcome.Changed, _service.Change("F20-0007", PaymentStatuses.Exempt, 4).Outcome);
            Assert.AreEqual("Unpaid", _payments.Rows[8].PaymentStatus);
            Assert.AreEqual("Exempt", _payments.Rows[7].PaymentStatus);
        }

        [TestMethod]
        public void UnknownPackage_IsNotFound()
        {
            var result = _service.Change("F20-9999", PaymentStatuses.Paid, 4);

            Assert.AreEqual(PaymentChangeOutcome.PackageNotFound, result.Outcome);
            Assert.AreEqual(0, _audit.Entries.Count);
        }

        [TestMethod]
        public void StatusNames_AreNormalised_AndOthersRefused()
        {
            string status;

            Assert.IsTrue(PaymentStatuses.TryNormalize(" paid ", out status));
            Assert.AreEqual("Paid", status);
            Assert.IsTrue(PaymentStatuses.TryNormalize("EXEMPT", out status));
            Assert.AreEqual("Exempt", status);
            Assert.IsFalse(PaymentStatuses.TryNormalize("Refunded", out status));
            Assert.IsFalse(PaymentStatuses.TryNormalize(null, out status));
        }

        [TestMethod]
        public void Defaults_WorkRelatedExempt_PersonalUnpaid()
        {
            Assert.AreEqual("Exempt", PaymentStatuses.DefaultFor("WorkRelated"));
            Assert.AreEqual("Unpaid", PaymentStatuses.DefaultFor("Personal"));
        }
    }
}
