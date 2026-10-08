using System;
using CourierService.Domain.Entities;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    /// <summary>T28: every attempt the worker makes, sent or failed, ends up in dbo.NotificationLog (IR-003).</summary>
    [TestClass]
    public class NotificationLogTests
    {
        private DateTime _now;
        private FakeNotificationQueue _queue;
        private FakePackages _packages;
        private FakeSender _email;
        private FakeNotificationLog _log;

        [TestInitialize]
        public void TestInitialize()
        {
            _now = new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);
            _queue = new FakeNotificationQueue(() => _now);
            _packages = new FakePackages();
            _email = new FakeSender(NotificationChannels.Email);
            _log = new FakeNotificationLog();
        }

        private NotificationProcessor Processor() => new NotificationProcessor(
            _queue, _packages, new PlainTextNotificationComposer(new FakeAppConfig()), new INotificationSender[] { _email },
            maxAttempts: 3, retryDelay: TimeSpan.FromSeconds(60), utcNow: () => _now, log: _log);

        [TestMethod]
        public void Sent_IsLogged_WithAddressSubjectAndChannel()
        {
            _packages.Add(7, "F20-0007", email: "thandi@courier.test");
            _queue.Add(7);

            Processor().ProcessNext();

            Assert.AreEqual(1, _log.Entries.Count);
            var entry = _log.Entries[0];
            Assert.AreEqual(7, entry.PackageId);
            Assert.AreEqual("Email", entry.Channel);
            Assert.AreEqual("thandi@courier.test", entry.RecipientAddress);
            Assert.AreEqual("Your package is ready for collection", entry.Subject);
            Assert.AreEqual("Sent", entry.Status);
            Assert.IsNull(entry.ErrorDetail);
        }

        [TestMethod]
        public void FailedAttemptThatWillBeRetried_IsLoggedAsFailed_AndSaysSo()
        {
            _packages.Add(7, "F20-0007");
            _queue.Add(7);
            _email.Results.Enqueue(() => NotificationSendResult.Failed("The mail server could not be used (GeneralFailure, ConnectionRefused)."));

            Processor().ProcessNext();

            Assert.AreEqual(1, _log.Entries.Count);
            Assert.AreEqual("Failed", _log.Entries[0].Status);
            Assert.AreEqual("The mail server could not be used (GeneralFailure, ConnectionRefused). Will try again (attempt 1 of 3).",
                _log.Entries[0].ErrorDetail);
        }

        [TestMethod]
        public void EveryAttempt_GetsItsOwnRow()
        {
            _packages.Add(7, "F20-0007");
            _queue.Add(7);
            for (var i = 0; i < 3; i++)
            {
                _email.Results.Enqueue(() => NotificationSendResult.Failed("Mail server down."));
            }

            var processor = Processor();
            for (var minute = 0; minute < 5; minute++)
            {
                processor.ProcessDue();
                _now = _now.AddMinutes(1);
            }

            Assert.AreEqual(3, _log.Entries.Count);
            Assert.IsTrue(_log.Entries[0].ErrorDetail.EndsWith("(attempt 1 of 3)."), _log.Entries[0].ErrorDetail);
            Assert.IsTrue(_log.Entries[1].ErrorDetail.EndsWith("(attempt 2 of 3)."), _log.Entries[1].ErrorDetail);
            Assert.AreEqual("Mail server down.", _log.Entries[2].ErrorDetail, "the last attempt gives up without a retry note");
            Assert.IsTrue(_log.Entries.TrueForAll(e => e.Status == "Failed"));
        }

        [TestMethod]
        public void RecipientWithoutEmail_IsLoggedAsFailed_WithAnEmptyAddress()
        {
            _packages.Add(7, "F20-0007", email: null);
            _queue.Add(7);

            Processor().ProcessNext();

            Assert.AreEqual(1, _log.Entries.Count);
            Assert.AreEqual("Failed", _log.Entries[0].Status);
            Assert.AreEqual(string.Empty, _log.Entries[0].RecipientAddress);
            Assert.AreEqual("The recipient has no email address.", _log.Entries[0].ErrorDetail);
        }

        [TestMethod]
        public void ChannelWithoutASender_IsLogged()
        {
            _packages.Add(7, "F20-0007");
            _queue.Add(7, channel: NotificationChannels.Sms);

            Processor().ProcessNext();

            Assert.AreEqual(1, _log.Entries.Count);
            Assert.AreEqual("SMS", _log.Entries[0].Channel);
            Assert.AreEqual("No sender is set up for the SMS channel.", _log.Entries[0].ErrorDetail);
        }

        [TestMethod]
        public void MissingPackage_IsNotLogged_BecauseTheLogNeedsAPackage()
        {
            _queue.Add(99);

            Assert.AreEqual(NotificationOutcome.Failed, Processor().ProcessNext());
            Assert.AreEqual(0, _log.Entries.Count);
        }

        [TestMethod]
        public void LogThatCannotBeWritten_DoesNotUndoTheSend()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7);
            _log.Throw = new InvalidOperationException("log table locked");

            var outcome = Processor().ProcessNext();

            Assert.AreEqual(NotificationOutcome.Sent, outcome);
            Assert.AreEqual("Sent", _queue.Get(item.NotificationQueueId).Status, "it must not be sent again");
            Assert.AreEqual(1, _email.Sent.Count);
        }

        [TestMethod]
        public void ErrorDetail_NeverContainsTheAddress()
        {
            _packages.Add(7, "F20-0007", email: "thandi@courier.test");
            _queue.Add(7);
            _email.Results.Enqueue(() => { throw new InvalidOperationException("Mailbox thandi@courier.test unavailable"); });

            Processor().ProcessNext();

            Assert.IsFalse(_log.Entries[0].ErrorDetail.Contains("thandi"), _log.Entries[0].ErrorDetail);
        }
    }
}