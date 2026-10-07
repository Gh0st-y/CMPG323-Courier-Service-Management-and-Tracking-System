using System;
using System.Net.Mail;
using System.Net.Sockets;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    [TestClass]
    public class SmtpNotificationSenderTests
    {
        private const string Address = "thandi@courier.test";

        /// <summary>Stands in for the mail server: remembers what it was given, or throws what it's told to.</summary>
        private sealed class FakeTransport : ISmtpTransport
        {
            public int Calls;
            public string To, FromAddress, FromName, Subject, Body;
            public bool IsBodyHtml;
            public Exception Throw;

            public void Send(MailMessage message, SmtpSettings settings)
            {
                Calls++;
                if (Throw != null)
                {
                    throw Throw;
                }

                // Copied here because the sender disposes the message afterwards
                To = message.To[0].Address;
                FromAddress = message.From.Address;
                FromName = message.From.DisplayName;
                Subject = message.Subject;
                Body = message.Body;
                IsBodyHtml = message.IsBodyHtml;
            }
        }

        private static SmtpSettings Settings() => new SmtpSettings
        {
            Host = "localhost",
            FromAddress = "courier-noreply@f20.local"
        };

        private static NotificationMessage Message(string to = Address) => new NotificationMessage
        {
            NotificationQueueId = 5,
            F20Identifier = "F20-0007",
            Channel = NotificationChannels.Email,
            TemplateKey = NotificationTemplateKeys.ReadyForCollection,
            To = to,
            Subject = "Your package is ready for collection",
            Body = "Hello Thandi,\r\n\r\nYour package F20-0007 is ready for collection."
        };

        private static void AssertNoAddress(NotificationSendResult result)
        {
            Assert.IsFalse((result.Error ?? string.Empty).Contains("thandi"), "the error must not contain the address: " + result.Error);
        }

        [TestMethod]
        public void HandlesTheEmailChannel()
        {
            Assert.AreEqual("Email", new SmtpNotificationSender(Settings(), new FakeTransport()).Channel);
        }

        [TestMethod]
        public void Success_SendsAPlainTextEmailFromTheConfiguredAddress()
        {
            var transport = new FakeTransport();

            var result = new SmtpNotificationSender(Settings(), transport).Send(Message());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, transport.Calls);
            Assert.AreEqual(Address, transport.To);
            Assert.AreEqual("courier-noreply@f20.local", transport.FromAddress);
            Assert.AreEqual("F20 Courier Service", transport.FromName);
            Assert.AreEqual("Your package is ready for collection", transport.Subject);
            Assert.IsTrue(transport.Body.Contains("F20-0007 is ready for collection"));
            Assert.IsFalse(transport.IsBodyHtml);
        }

        [TestMethod]
        public void InvalidAddress_IsAPermanentFailure_AndNothingIsSent()
        {
            var transport = new FakeTransport();

            var result = new SmtpNotificationSender(Settings(), transport).Send(Message("thandi at courier"));

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Permanent);
            Assert.AreEqual(0, transport.Calls);
            AssertNoAddress(result);
        }

        [TestMethod]
        public void MailboxRejected_IsAPermanentFailure_WithoutTheAddress()
        {
            var transport = new FakeTransport { Throw = new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "<" + Address + ">") };

            var result = new SmtpNotificationSender(Settings(), transport).Send(Message());

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Permanent);
            Assert.IsTrue(result.Error.Contains("550"), result.Error);
            AssertNoAddress(result);
        }

        [TestMethod]
        public void MailboxBusy_IsWorthARetry()
        {
            var transport = new FakeTransport { Throw = new SmtpFailedRecipientException(SmtpStatusCode.MailboxBusy, "<" + Address + ">") };

            var result = new SmtpNotificationSender(Settings(), transport).Send(Message());

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Permanent);
            Assert.IsTrue(result.Error.Contains("450"), result.Error);
            AssertNoAddress(result);
        }

        [TestMethod]
        public void ServerNotRunning_IsWorthARetry_AndSaysWhy()
        {
            var transport = new FakeTransport
            {
                Throw = new SmtpException("Failure sending mail.", new SocketException((int)SocketError.ConnectionRefused))
            };

            var result = new SmtpNotificationSender(Settings(), transport).Send(Message());

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Permanent);
            Assert.IsTrue(result.Error.Contains("ConnectionRefused"), result.Error);
        }

        [TestMethod]
        public void AnyOtherException_IsWorthARetry_AndDoesNotEscape()
        {
            var transport = new FakeTransport { Throw = new InvalidOperationException("Mailbox for " + Address + " is odd") };

            var result = new SmtpNotificationSender(Settings(), transport).Send(Message());

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Permanent);
            AssertNoAddress(result);
        }

        [TestMethod]
        public void NotSetUp_IsWorthARetry_AndNothingIsSent()
        {
            var transport = new FakeTransport();
            var settings = Settings();
            settings.Host = null;

            var result = new SmtpNotificationSender(settings, transport).Send(Message());

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Permanent);
            Assert.AreEqual("Email is not set up: Smtp.Host is not set.", result.Error);
            Assert.AreEqual(0, transport.Calls);
        }
    }
}