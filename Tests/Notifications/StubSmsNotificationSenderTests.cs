
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    [TestClass]
    public class StubSmsNotificationSenderTests
    {
        [TestMethod]
        public void HandlesTheSmsChannel()
        {
            var sender = new StubSmsNotificationSender();

            Assert.AreEqual(NotificationChannels.Sms, sender.Channel);
        }

        [TestMethod]
        public void ValidMessage_ReturnsSuccess()
        {
            var sender = new StubSmsNotificationSender();
            var message = new NotificationMessage
            {
                NotificationQueueId = 12,
                PackageId = 7,
                F20Identifier = "F20-0007",
                Channel = NotificationChannels.Sms,
                TemplateKey = NotificationTemplateKeys.ReadyForCollection,
                To = "0123456789",
                Subject = "Package ready",
                Body = "Your package is ready for collection."
            };

            var result = sender.Send(message);

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void MissingMessage_ReturnsPermanentFailure()
        {
            var sender = new StubSmsNotificationSender();

            var result = sender.Send(null);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Permanent);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
            Assert.IsFalse(result.Error.Contains("0123456789"));
        }
    }
}
