using System;
using CourierService.Domain.Entities;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    [TestClass]
    public class PlainTextNotificationComposerTests
    {
        private static NotificationQueueItem Item(string templateKey, string channel = NotificationChannels.Email) =>
            new NotificationQueueItem { NotificationQueueId = 5, PackageId = 7, Channel = channel, TemplateKey = templateKey };

        [TestMethod]
        public void SubjectComesFromAppConfig()
        {
            var config = new FakeAppConfig();
            config.Values["Notification.ReadyForCollection.Subject"] = "Come and fetch it";
            var package = new FakePackages().Add(7, "F20-0007");

            var message = new PlainTextNotificationComposer(config).Compose(Item(NotificationTemplateKeys.ReadyForCollection), package);

            Assert.AreEqual("Come and fetch it", message.Subject);
        }

        [TestMethod]
        public void SubjectFallsBackWhenTheSettingIsMissing()
        {
            var composer = new PlainTextNotificationComposer(new FakeAppConfig());

            Assert.AreEqual("Your package is ready for collection", composer.SubjectFor(NotificationTemplateKeys.ReadyForCollection));
            Assert.AreEqual("Your package has been collected", composer.SubjectFor(NotificationTemplateKeys.Collected));
        }

        [TestMethod]
        public void ReadyEmail_HasTheNameNumberAndShelf()
        {
            var package = new FakePackages().Add(7, "F20-0007", email: " thandi@courier.test ", fullName: "Thandi Mokoena", storageLocationCode: "Shelf B-04");

            var message = new PlainTextNotificationComposer(new FakeAppConfig()).Compose(Item(NotificationTemplateKeys.ReadyForCollection), package);

            Assert.AreEqual("thandi@courier.test", message.To);
            Assert.AreEqual(5, message.NotificationQueueId);
            Assert.IsTrue(message.Body.Contains("Hello Thandi Mokoena,"), message.Body);
            Assert.IsTrue(message.Body.Contains("F20-0007 is ready for collection at Shelf B-04."), message.Body);
        }

        [TestMethod]
        public void CollectedEmail_HasTheCollectionTime()
        {
            var package = new FakePackages().Add(7, "F20-0007", collectedAtUtc: new DateTime(2026, 10, 7, 9, 5, 0, DateTimeKind.Utc));

            var message = new PlainTextNotificationComposer(new FakeAppConfig()).Compose(Item(NotificationTemplateKeys.Collected), package);

            Assert.IsTrue(message.Body.Contains("F20-0007 was collected on 2026-10-07 09:05 (UTC)."), message.Body);
        }

        [TestMethod]
        public void Sms_GoesToThePhone_AndLeavesOutTheName()
        {
            var package = new FakePackages().Add(7, "F20-0007", phone: "0821234567", fullName: "Thandi Mokoena");

            var message = new PlainTextNotificationComposer(new FakeAppConfig())
                .Compose(Item(NotificationTemplateKeys.ReadyForCollection, NotificationChannels.Sms), package);

            Assert.AreEqual("0821234567", message.To);
            Assert.IsFalse(message.Body.Contains("Thandi"));
            Assert.IsTrue(message.Body.Length <= 160, "fits in one SMS: " + message.Body.Length);
        }

        [TestMethod]
        public void MissingAddress_GivesAnEmptyTo()
        {
            var package = new FakePackages().Add(7, "F20-0007", email: null, phone: null);
            var composer = new PlainTextNotificationComposer(new FakeAppConfig());

            Assert.AreEqual(string.Empty, composer.Compose(Item(NotificationTemplateKeys.ReadyForCollection), package).To);
            Assert.AreEqual(string.Empty, composer.Compose(Item(NotificationTemplateKeys.ReadyForCollection, NotificationChannels.Sms), package).To);
        }


        [TestMethod]
        public void ReadyEmail_UsesConfiguredBodyAndReplacesPlaceholders()
        {
            var config = new FakeAppConfig();
            config.Values["Notification.ReadyForCollection.Body"] =
                "Hello {{RecipientName}}. Package {{PackageId}} is ready at {{StorageLocation}}.";

            var package = new FakePackages().Add(
                7,
                "F20-0007",
                email: "thandi@courier.test",
                fullName: "Thandi Mokoena",
                storageLocationCode: "Shelf B-04");

            var message = new PlainTextNotificationComposer(config).Compose(
                Item(NotificationTemplateKeys.ReadyForCollection),
                package);

            Assert.AreEqual(
                "Hello Thandi Mokoena. Package F20-0007 is ready at Shelf B-04.",
                message.Body);
        }

        [TestMethod]
        public void CollectedEmail_UsesConfiguredBodyAndReplacesPlaceholders()
        {
            var config = new FakeAppConfig();
            config.Values["Notification.Collected.Body"] =
                "Hello {{RecipientName}}. Package {{PackageId}} was collected at {{CollectionTime}}.";

            var package = new FakePackages().Add(
                7,
                "F20-0007",
                fullName: "Thandi Mokoena",
                collectedAtUtc: new DateTime(
                    2026, 10, 7, 9, 5, 0, DateTimeKind.Utc));

            var message = new PlainTextNotificationComposer(config).Compose(
                Item(NotificationTemplateKeys.Collected),
                package);

            Assert.AreEqual(
                "Hello Thandi Mokoena. Package F20-0007 was collected at 2026-10-07 09:05 (UTC).",
                message.Body);
        }

    }
}