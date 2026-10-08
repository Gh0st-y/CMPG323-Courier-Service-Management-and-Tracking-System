using System;
using System.Collections.Generic;
using System.Diagnostics;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Notifications
{
    /// <summary>What happened to one queue item.</summary>
    public enum NotificationOutcome
    {
        /// <summary>Nothing was due.</summary>
        Idle,
        Sent,

        /// <summary>The attempt failed and the item stays Pending for another try after the retry delay.</summary>
        WillRetry,

        /// <summary>Gave up: a permanent problem, or the last allowed attempt failed. The item is marked Failed.</summary>
        Failed
    }

    /// <summary>
    /// Works through dbo.NotificationQueue (T25): takes the oldest due item, builds the message, hands it to the sender
    /// for its channel and records the result. A failed attempt stays Pending and is tried again after
    /// <see cref="RetryDelay"/>, up to <see cref="MaxAttempts"/> attempts in total, then it is marked Failed.
    ///
    /// This only runs in the background worker, never in a staff request, so a slow or broken SMTP server can't hold up
    /// or fail a status change (NFR-022). Nothing it logs contains the recipient's name, address or phone number (SR-03).
    ///
    /// Every attempt, sent or failed, is also written to dbo.NotificationLog (T28, IR-003), which the package detail
    /// page lists (DR-012). That row does hold the address, as the schema intends; the page masks it.
    /// </summary>
    public class NotificationProcessor
    {
        public const int DefaultMaxAttempts = 3;
        public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(60);

        /// <summary>Upper limit for one ProcessDue call, so one run can't go on forever.</summary>
        public const int DefaultBatchSize = 50;

        private readonly INotificationRepository _queue;
        private readonly IPackageRepository _packages;
        private readonly INotificationComposer _composer;
        private readonly Dictionary<string, INotificationSender> _senders;
        private readonly Func<DateTime> _utcNow;
        private readonly INotificationLogRepository _log;

        public NotificationProcessor(
            INotificationRepository queue,
            IPackageRepository packages,
            INotificationComposer composer,
            IEnumerable<INotificationSender> senders,
            int maxAttempts = DefaultMaxAttempts,
            TimeSpan? retryDelay = null,
            Func<DateTime> utcNow = null,
            INotificationLogRepository log = null)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (composer == null) throw new ArgumentNullException(nameof(composer));
            if (senders == null) throw new ArgumentNullException(nameof(senders));
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts), "At least one attempt is needed.");

            _queue = queue;
            _packages = packages;
            _composer = composer;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _log = log;
            MaxAttempts = maxAttempts;
            RetryDelay = retryDelay ?? DefaultRetryDelay;

            _senders = new Dictionary<string, INotificationSender>(StringComparer.OrdinalIgnoreCase);
            foreach (var sender in senders)
            {
                if (sender != null)
                {
                    _senders[sender.Channel] = sender;
                }
            }
        }

        public int MaxAttempts { get; }

        public TimeSpan RetryDelay { get; }

        /// <summary>Processes due items until none are left or <paramref name="batchSize"/> is reached. Returns how many were processed.</summary>
        public int ProcessDue(int batchSize = DefaultBatchSize)
        {
            var processed = 0;
            while (processed < batchSize && ProcessNext() != NotificationOutcome.Idle)
            {
                processed++;
            }

            return processed;
        }

        /// <summary>
        /// Processes the oldest due item, if there is one. Problems with the item itself or its sender are recorded
        /// against the item. Database errors are not caught here: the worker logs them and tries again on its next run.
        /// </summary>
        public NotificationOutcome ProcessNext()
        {
            var item = _queue.GetNextDue(_utcNow() - RetryDelay);
            if (item == null)
            {
                return NotificationOutcome.Idle;
            }

            var package = _packages.GetById(item.PackageId);
            if (package == null)
            {
                // No log row: dbo.NotificationLog needs an existing package
                return GiveUp(item, "The package no longer exists.", null, writeLog: false);
            }

            INotificationSender sender;
            if (!_senders.TryGetValue(item.Channel ?? string.Empty, out sender))
            {
                return GiveUp(item, "No sender is set up for the " + item.Channel + " channel.", null);
            }

            NotificationMessage message;
            try
            {
                message = _composer.Compose(item, package);
            }
            catch (Exception ex)
            {
                return GiveUp(item, "The message could not be built (" + ex.GetType().Name + ").", null);
            }

            if (message == null || string.IsNullOrWhiteSpace(message.To))
            {
                var missing = string.Equals(item.Channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase)
                    ? "phone number"
                    : "email address";
                return GiveUp(item, "The recipient has no " + missing + ".", message);
            }

            NotificationSendResult result;
            try
            {
                result = sender.Send(message) ?? NotificationSendResult.Failed("The sender returned no result.");
            }
            catch (Exception ex)
            {
                // Only the type: some mail exceptions repeat the server's reply, which can include the address (SR-03)
                result = NotificationSendResult.Failed("The sender threw " + ex.GetType().Name + ".");
            }

            if (result.Success)
            {
                _queue.MarkSent(item.NotificationQueueId);
                WriteLog(item, message, NotificationLogEntry.StatusSent, null);
                Trace.TraceInformation("Notification {0} ({1}, {2}) for {3} sent.",
                    item.NotificationQueueId, item.TemplateKey, item.Channel, package.F20Identifier);
                return NotificationOutcome.Sent;
            }

            var attempt = item.AttemptCount + 1;
            if (result.Permanent || attempt >= MaxAttempts)
            {
                return GiveUp(item, result.Error, message);
            }

            _queue.RecordFailedAttempt(item.NotificationQueueId);
            WriteLog(item, message, NotificationLogEntry.StatusFailed,
                result.Error + " Will try again (attempt " + attempt + " of " + MaxAttempts + ").");
            Trace.TraceWarning("Notification {0} ({1}, {2}) attempt {3} of {4} failed, will retry: {5}",
                item.NotificationQueueId, item.TemplateKey, item.Channel, attempt, MaxAttempts, result.Error);
            return NotificationOutcome.WillRetry;
        }

        private NotificationOutcome GiveUp(NotificationQueueItem item, string reason, NotificationMessage message, bool writeLog = true)
        {
            _queue.MarkFailed(item.NotificationQueueId);
            if (writeLog)
            {
                WriteLog(item, message, NotificationLogEntry.StatusFailed, reason);
            }

            Trace.TraceWarning("Notification {0} ({1}, {2}) failed after {3} attempt(s), not retrying: {4}",
                item.NotificationQueueId, item.TemplateKey, item.Channel, item.AttemptCount + 1, reason);
            return NotificationOutcome.Failed;
        }

        /// <summary>
        /// One dbo.NotificationLog row for this attempt. Written after the queue row is updated, and a log that can't be
        /// written is only traced: the attempt is already recorded on the queue, and failing here would mean sending again.
        /// </summary>
        private void WriteLog(NotificationQueueItem item, NotificationMessage message, string status, string errorDetail)
        {
            if (_log == null)
            {
                return;
            }

            try
            {
                _log.Add(new NotificationLogEntry
                {
                    PackageId = item.PackageId,
                    Channel = item.Channel,
                    RecipientAddress = message == null ? string.Empty : (message.To ?? string.Empty),
                    Subject = message == null ? null : message.Subject,
                    Status = status,
                    ErrorDetail = errorDetail
                });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Notification {0}: the notification log could not be written ({1}).",
                    item.NotificationQueueId, ex.GetType().Name);
            }
        }
    }
}