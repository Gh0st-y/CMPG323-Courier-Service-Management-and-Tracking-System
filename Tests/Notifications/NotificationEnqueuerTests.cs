using System;
using CourierService.Domain.Entities;
using CourierService.Services.Notifications;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    [TestClass]
    public class NotificationEnqueuerTests
    {
        private static PackageStatusChange Change(PackageStatus from, PackageStatus to) => new PackageStatusChange
        {
            PackageId = 42,
            F20Identifier = "F20-0042",
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = 3,
            ChangedAtUtc = DateTime.UtcNow
        };

        [TestMethod]
        public void ReadyForCollection_QueuesOneEmail_InTheSameUnitOfWork()
        {
            var queue = new FakeNotificationQueue();
            var unitOfWork = new FakeUnitOfWork();

            new NotificationEnqueuer(queue).OnStatusChanged(Change(PackageStatus.InStorage, PackageStatus.ReadyForCollection), unitOfWork);

            Assert.AreEqual(1, queue.Items.Count);
            Assert.AreEqual(42, queue.Items[0].PackageId);
            Assert.AreEqual(NotificationChannels.Email, queue.Items[0].Channel);
            Assert.AreEqual(NotificationTemplateKeys.ReadyForCollection, queue.Items[0].TemplateKey);
            Assert.AreEqual("Pending", queue.Items[0].Status);
            Assert.AreSame(unitOfWork, queue.UnitsOfWorkUsed[0], "the row must be part of the status change's transaction");
        }

        [TestMethod]
        public void Collected_QueuesTheCollectedEmail()
        {
            var queue = new FakeNotificationQueue();

            new NotificationEnqueuer(queue).OnStatusChanged(Change(PackageStatus.ReadyForCollection, PackageStatus.Collected), new FakeUnitOfWork());

            Assert.AreEqual(1, queue.Items.Count);
            Assert.AreEqual(NotificationTemplateKeys.Collected, queue.Items[0].TemplateKey);
        }

        [TestMethod]
        public void InStorage_QueuesNothing()
        {
            var queue = new FakeNotificationQueue();

            new NotificationEnqueuer(queue).OnStatusChanged(Change(PackageStatus.Registered, PackageStatus.InStorage), new FakeUnitOfWork());

            Assert.AreEqual(0, queue.Items.Count);
        }

        [TestMethod]
        public void WithSmsEnabled_QueuesEmailAndSms()
        {
            var queue = new FakeNotificationQueue();

            new NotificationEnqueuer(queue, smsEnabled: true).OnStatusChanged(Change(PackageStatus.InStorage, PackageStatus.ReadyForCollection), new FakeUnitOfWork());

            Assert.AreEqual(2, queue.Items.Count);
            Assert.AreEqual(NotificationChannels.Email, queue.Items[0].Channel);
            Assert.AreEqual(NotificationChannels.Sms, queue.Items[1].Channel);
        }

        [TestMethod]
        public void TemplateFor_OnlyTheStatusesThatTellTheRecipientSomething()
        {
            Assert.IsNull(NotificationEnqueuer.TemplateFor(PackageStatus.Registered));
            Assert.IsNull(NotificationEnqueuer.TemplateFor(PackageStatus.InStorage));
            Assert.AreEqual("ReadyForCollection", NotificationEnqueuer.TemplateFor(PackageStatus.ReadyForCollection));
            Assert.AreEqual("Collected", NotificationEnqueuer.TemplateFor(PackageStatus.Collected));
        }
    }
}