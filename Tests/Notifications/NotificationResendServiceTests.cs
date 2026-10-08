using System;
using System.Collections.Generic;
using CourierService.Domain;
using CourierService.Services.Audit;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    [TestClass]
    public class NotificationResendServiceTests
    {
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

            public IUnitOfWork Begin()
            {
                var unitOfWork = new TrackingUnitOfWork();
                Started.Add(unitOfWork);
                return unitOfWork;
            }
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

        private DateTime _now;
        private FakeNotificationQueue _queue;
        private FakePackages _packages;
        private FakeAuditLogger _audit;
        private TrackingUnitOfWorkFactory _unitsOfWork;
        private NotificationResendService _service;

        [TestInitialize]
        public void TestInitialize()
        {
            _now = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
            _queue = new FakeNotificationQueue(() => _now);
            _packages = new FakePackages();
            _audit = new FakeAuditLogger();
            _unitsOfWork = new TrackingUnitOfWorkFactory();
            _service = new NotificationResendService(_packages, _queue, _audit, _unitsOfWork, () => _now);

            _packages.Add(7, "F20-0007", email: "thandi@courier.test");
        }

        private int FailedItem(string templateKey = NotificationTemplateKeys.ReadyForCollection, string channel = NotificationChannels.Email)
        {
            var item = _queue.Add(7, channel, templateKey, attemptCount: 3, lastAttemptAtUtc: _now.AddMinutes(-1));
            item.Status = "Failed";
            return item.NotificationQueueId;
        }

        [TestMethod]
        public void FailedNotification_IsRequeued_WithAFreshSetOfAttempts()
        {
            var id = FailedItem();

            var result = _service.Resend("F20-0007", NotificationChannels.Email, 5);

            Assert.AreEqual(ResendOutcome.Requeued, result.Outcome);
            Assert.AreEqual(id, result.NotificationQueueId);
            Assert.AreEqual("ReadyForCollection", result.TemplateKey);
            Assert.AreEqual(_now, result.QueuedAtUtc);

            var stored = _queue.Get(id);
            Assert.AreEqual("Pending", stored.Status);
            Assert.AreEqual(0, stored.AttemptCount);
            Assert.IsNull(stored.LastAttemptAtUtc, "due straight away");
        }

        [TestMethod]
        public void Requeued_ItemIsPickedUpByTheWorkerStraightAway()
        {
            var id = FailedItem();
            _service.Resend("F20-0007", NotificationChannels.Email, 5);

            var due = _queue.GetNextDue(_now.AddSeconds(-60));

            Assert.IsNotNull(due);
            Assert.AreEqual(id, due.NotificationQueueId);
        }

        [TestMethod]
        public void Resend_IsAudited_InTheSameTransaction_WithoutPersonalData()
        {
            var id = FailedItem();

            _service.Resend("F20-0007", NotificationChannels.Email, 5);

            Assert.AreEqual(1, _audit.Entries.Count);
            var entry = _audit.Entries[0];
            Assert.AreEqual(AuditActions.NotificationResent, entry[0]);
            Assert.AreEqual(AuditEntityTypes.Package, entry[1]);
            Assert.AreEqual("F20-0007", entry[2]);
            Assert.AreEqual("Email ReadyForCollection notification re-queued (queue item " + id + ")", entry[3]);
            Assert.AreEqual("5", entry[4]);
            Assert.IsFalse(entry[3].Contains("thandi"));

            Assert.AreEqual(1, _unitsOfWork.Started.Count);
            Assert.IsTrue(_unitsOfWork.Started[0].Committed);
            Assert.AreSame(_unitsOfWork.Started[0], _audit.UnitsOfWork[0]);
        }

        [TestMethod]
        public void UnknownPackage_IsNotFound()
        {
            Assert.AreEqual(ResendOutcome.PackageNotFound, _service.Resend("F20-9999", NotificationChannels.Email, 5).Outcome);
            Assert.AreEqual(0, _audit.Entries.Count);
        }

        [TestMethod]
        public void PackageWithoutANotificationOnThatChannel_HasNothingToResend()
        {
            FailedItem(channel: NotificationChannels.Email);

            Assert.AreEqual(ResendOutcome.NoNotification, _service.Resend("F20-0007", NotificationChannels.Sms, 5).Outcome);
            Assert.AreEqual(0, _audit.Entries.Count);
        }

        [TestMethod]
        public void LatestNotificationWasSent_IsNotResent()
        {
            var item = _queue.Add(7);
            item.Status = "Sent";

            var result = _service.Resend("F20-0007", NotificationChannels.Email, 5);

            Assert.AreEqual(ResendOutcome.NotFailed, result.Outcome);
            Assert.AreEqual("Sent", result.CurrentStatus);
            Assert.AreEqual("Sent", _queue.Get(item.NotificationQueueId).Status);
            Assert.AreEqual(0, _audit.Entries.Count);
            Assert.IsFalse(_unitsOfWork.Started[0].Committed);
        }

        [TestMethod]
        public void OldFailure_IsNotResent_AfterANewerNotification()
        {
            // "Ready for collection" failed, then the package was collected and that email went out
            var failedId = FailedItem(NotificationTemplateKeys.ReadyForCollection);
            var collected = _queue.Add(7, templateKey: NotificationTemplateKeys.Collected);
            collected.Status = "Sent";

            var result = _service.Resend("F20-0007", NotificationChannels.Email, 5);

            Assert.AreEqual(ResendOutcome.NotFailed, result.Outcome);
            Assert.AreEqual("Failed", _queue.Get(failedId).Status, "the old 'ready' email must not go out after 'collected'");
        }

        [TestMethod]
        public void SecondResend_FindsItAlreadyQueued()
        {
            FailedItem();

            Assert.AreEqual(ResendOutcome.Requeued, _service.Resend("F20-0007", NotificationChannels.Email, 5).Outcome);

            var second = _service.Resend("F20-0007", NotificationChannels.Email, 5);
            Assert.AreEqual(ResendOutcome.NotFailed, second.Outcome);
            Assert.AreEqual("Pending", second.CurrentStatus);
            Assert.AreEqual(1, _audit.Entries.Count);
        }

        [TestMethod]
        public void Channel_IsNormalised_AndDefaultsToEmail()
        {
            string channel;

            Assert.IsTrue(NotificationResendService.TryNormalizeChannel(null, out channel));
            Assert.AreEqual("Email", channel);
            Assert.IsTrue(NotificationResendService.TryNormalizeChannel("  email ", out channel));
            Assert.AreEqual("Email", channel);
            Assert.IsTrue(NotificationResendService.TryNormalizeChannel("sms", out channel));
            Assert.AreEqual("SMS", channel);
            Assert.IsFalse(NotificationResendService.TryNormalizeChannel("fax", out channel));
            Assert.IsNull(channel);
        }
    }
}