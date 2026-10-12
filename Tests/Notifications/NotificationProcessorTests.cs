using System;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    [TestClass]
    public class NotificationProcessorTests
    {
        private DateTime _now;
        private FakeNotificationQueue _queue;
        private FakePackages _packages;
        private FakeAppConfig _config;
        private FakeSender _email;

        [TestInitialize]
        public void TestInitialize()
        {
            _now = new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);
            _queue = new FakeNotificationQueue(() => _now);
            _packages = new FakePackages();
            _config = new FakeAppConfig();
            _email = new FakeSender(NotificationChannels.Email);
        }

        private NotificationProcessor Processor(params INotificationSender[] senders) => new NotificationProcessor(
            _queue, _packages, new PlainTextNotificationComposer(_config),
            senders.Length == 0 ? new INotificationSender[] { _email } : senders,
            maxAttempts: 3, retryDelay: TimeSpan.FromSeconds(60), utcNow: () => _now);

        [TestMethod]
        public void NothingDue_IsIdle()
        {
            Assert.AreEqual(NotificationOutcome.Idle, Processor().ProcessNext());
            Assert.AreEqual(0, _email.Sent.Count);
        }

        [TestMethod]
        public void Success_SendsToTheRecipientsEmail_AndMarksSent()
        {
            _packages.Add(7, "F20-0007", email: "thandi@courier.test");
            var item = _queue.Add(7);

            Assert.AreEqual(NotificationOutcome.Sent, Processor().ProcessNext());

            Assert.AreEqual(1, _email.Sent.Count);
            Assert.AreEqual("thandi@courier.test", _email.Sent[0].To);
            Assert.AreEqual("F20-0007", _email.Sent[0].F20Identifier);
            Assert.AreEqual("Sent", _queue.Get(item.NotificationQueueId).Status);
            Assert.AreEqual(1, _queue.Get(item.NotificationQueueId).AttemptCount);
        }

        [TestMethod]
        public void AsksOnlyForItemsWhoseRetryDelayHasPassed()
        {
            Processor().ProcessNext();

            Assert.AreEqual(_now.AddSeconds(-60), _queue.LastRetryBeforeUtc);
        }

        [TestMethod]
        public void FirstFailure_StaysPendingForARetry()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7);
            _email.Results.Enqueue(() => NotificationSendResult.Failed("SMTP server not reachable"));

            Assert.AreEqual(NotificationOutcome.WillRetry, Processor().ProcessNext());

            var stored = _queue.Get(item.NotificationQueueId);
            Assert.AreEqual("Pending", stored.Status);
            Assert.AreEqual(1, stored.AttemptCount);
        }

        [TestMethod]
        public void FailedItem_IsNotRetriedBeforeTheDelay_ThenIsRetried()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7);
            _email.Results.Enqueue(() => NotificationSendResult.Failed("SMTP server not reachable"));
            var processor = Processor();

            processor.ProcessNext();                                       // fails at 08:00:00
            _now = _now.AddSeconds(30);
            Assert.AreEqual(NotificationOutcome.Idle, processor.ProcessNext(), "30 s later is too soon");

            _now = _now.AddSeconds(30);
            Assert.AreEqual(NotificationOutcome.Sent, processor.ProcessNext(), "60 s later it is tried again");
            Assert.AreEqual(2, _email.Sent.Count);
            Assert.AreEqual(2, _queue.Get(item.NotificationQueueId).AttemptCount);
        }

        [TestMethod]
        public void ThirdFailure_GivesUp_AndMarksFailed()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7, attemptCount: 2, lastAttemptAtUtc: _now.AddMinutes(-5));
            _email.Results.Enqueue(() => NotificationSendResult.Failed("SMTP server not reachable"));

            Assert.AreEqual(NotificationOutcome.Failed, Processor().ProcessNext());

            var stored = _queue.Get(item.NotificationQueueId);
            Assert.AreEqual("Failed", stored.Status);
            Assert.AreEqual(3, stored.AttemptCount);
        }

        [TestMethod]
        public void ProcessDue_RetriesUpToThreeAttemptsInTotal()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7);
            for (var i = 0; i < 5; i++)
            {
                _email.Results.Enqueue(() => NotificationSendResult.Failed("SMTP server not reachable"));
            }

            var processor = Processor();
            for (var minute = 0; minute < 10; minute++)
            {
                processor.ProcessDue();
                _now = _now.AddMinutes(1);
            }

            Assert.AreEqual(3, _email.Sent.Count, "attempts");
            Assert.AreEqual("Failed", _queue.Get(item.NotificationQueueId).Status);
            Assert.AreEqual(3, _queue.Get(item.NotificationQueueId).AttemptCount);
        }

        [TestMethod]
        public void PermanentFailure_GivesUpStraightAway()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7);
            _email.Results.Enqueue(() => NotificationSendResult.Failed("Mailbox does not exist", permanent: true));

            Assert.AreEqual(NotificationOutcome.Failed, Processor().ProcessNext());
            Assert.AreEqual("Failed", _queue.Get(item.NotificationQueueId).Status);
            Assert.AreEqual(1, _queue.Get(item.NotificationQueueId).AttemptCount);
        }

        [TestMethod]
        public void SenderThatThrows_CountsAsAFailedAttempt_AndDoesNotEscape()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7);
            _email.Results.Enqueue(() => { throw new InvalidOperationException("Mailbox unavailable for thandi@courier.test"); });

            Assert.AreEqual(NotificationOutcome.WillRetry, Processor().ProcessNext());
            Assert.AreEqual("Pending", _queue.Get(item.NotificationQueueId).Status);
            Assert.AreEqual(1, _queue.Get(item.NotificationQueueId).AttemptCount);
        }

        [TestMethod]
        public void RecipientWithoutEmail_FailsWithoutSending()
        {
            _packages.Add(7, "F20-0007", email: null);
            var item = _queue.Add(7);

            Assert.AreEqual(NotificationOutcome.Failed, Processor().ProcessNext());
            Assert.AreEqual(0, _email.Sent.Count);
            Assert.AreEqual("Failed", _queue.Get(item.NotificationQueueId).Status);
        }

        [TestMethod]
        public void ChannelWithoutASender_Fails()
        {
            _packages.Add(7, "F20-0007");
            var item = _queue.Add(7, channel: NotificationChannels.Sms);

            Assert.AreEqual(NotificationOutcome.Failed, Processor().ProcessNext());
            Assert.AreEqual(0, _email.Sent.Count);
            Assert.AreEqual("Failed", _queue.Get(item.NotificationQueueId).Status);
        }

        [TestMethod]
        public void SmsGoesToTheSmsSender_AndThePhoneNumber()
        {
            var sms = new FakeSender(NotificationChannels.Sms);
            _packages.Add(7, "F20-0007", phone: "0821234567");
            _queue.Add(7, channel: NotificationChannels.Sms);

            Assert.AreEqual(NotificationOutcome.Sent, Processor(_email, sms).ProcessNext());
            Assert.AreEqual(0, _email.Sent.Count);
            Assert.AreEqual("0821234567", sms.Sent[0].To);
        }


        [TestMethod]
        public void SmsFailure_DoesNotPreventEmailFromBeingProcessed()
        {
            var sms = new FakeSender(NotificationChannels.Sms);
            sms.Results.Enqueue(() => NotificationSendResult.Failed("SMS provider unavailable"));

            _packages.Add(7, "F20-0007", email: "thandi@courier.test", phone: "0821234567");

            var emailItem = _queue.Add(7, channel: NotificationChannels.Email);
            var smsItem = _queue.Add(7, channel: NotificationChannels.Sms);

            var processor = Processor(_email, sms);

            // Process the email notification first.
            Assert.AreEqual(NotificationOutcome.Sent, processor.ProcessNext());

            // The SMS failure is handled independently.
            Assert.AreEqual(NotificationOutcome.WillRetry, processor.ProcessNext());

            Assert.AreEqual(1, _email.Sent.Count);
            Assert.AreEqual("Sent", _queue.Get(emailItem.NotificationQueueId).Status);
            Assert.AreEqual("Pending", _queue.Get(smsItem.NotificationQueueId).Status);
            Assert.AreEqual(1, sms.Sent.Count);
        }

        [TestMethod]
        public void MissingPackage_Fails()
        {
            var item = _queue.Add(99);

            Assert.AreEqual(NotificationOutcome.Failed, Processor().ProcessNext());
            Assert.AreEqual("Failed", _queue.Get(item.NotificationQueueId).Status);
        }

        [TestMethod]
        public void ProcessDue_SendsEverythingDue_OldestFirst()
        {
            _packages.Add(7, "F20-0007");
            _packages.Add(8, "F20-0008");
            _queue.Add(7);
            _now = _now.AddSeconds(1);
            _queue.Add(8, templateKey: NotificationTemplateKeys.Collected);

            Assert.AreEqual(2, Processor().ProcessDue());
            Assert.AreEqual("F20-0007", _email.Sent[0].F20Identifier);
            Assert.AreEqual("F20-0008", _email.Sent[1].F20Identifier);
        }

        [TestMethod]
        public void ProcessDue_StopsAtTheBatchSize()
        {
            for (var id = 1; id <= 5; id++)
            {
                _packages.Add(id, "F20-000" + id);
                _queue.Add(id);
            }

            Assert.AreEqual(2, Processor().ProcessDue(batchSize: 2));
            Assert.AreEqual(2, _email.Sent.Count);
        }
    }
}