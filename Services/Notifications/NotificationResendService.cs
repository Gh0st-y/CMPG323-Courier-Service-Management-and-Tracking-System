using System;
using CourierService.Domain;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;

namespace CourierService.Services.Notifications
{
    public enum ResendOutcome
    {
        /// <summary>The failed notification is Pending again; the worker sends it within a few seconds.</summary>
        Requeued,
        PackageNotFound,

        /// <summary>The package has never had a notification on this channel.</summary>
        NoNotification,

        /// <summary>The package's latest notification on this channel didn't fail (it was sent, or is still waiting).</summary>
        NotFailed
    }

    public class ResendResult
    {
        public ResendOutcome Outcome { get; set; }
        public int NotificationQueueId { get; set; }
        public string Channel { get; set; }
        public string TemplateKey { get; set; }

        /// <summary>For NotFailed: the latest notification's status, e.g. "Sent" or "Pending".</summary>
        public string CurrentStatus { get; set; }

        public DateTime QueuedAtUtc { get; set; }
    }

    /// <summary>
    /// Staff resend of a notification that failed (T49, NFR-022: "allowing staff to retry"). It re-queues the
    /// package's latest notification on the chosen channel, only if that one failed, so an old "ready for collection"
    /// email can't be sent after a newer "collected" one. The worker (T25) then sends it with a fresh set of attempts,
    /// and every attempt is logged as usual (T28). The resend itself goes in the audit log (SR-04).
    /// </summary>
    public class NotificationResendService
    {
        private readonly IPackageRepository _packages;
        private readonly INotificationRepository _queue;
        private readonly IAuditLogger _auditLogger;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;
        private readonly Func<DateTime> _utcNow;

        public NotificationResendService(
            IPackageRepository packages,
            INotificationRepository queue,
            IAuditLogger auditLogger,
            IUnitOfWorkFactory unitOfWorkFactory,
            Func<DateTime> utcNow = null)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            if (auditLogger == null) throw new ArgumentNullException(nameof(auditLogger));
            if (unitOfWorkFactory == null) throw new ArgumentNullException(nameof(unitOfWorkFactory));

            _packages = packages;
            _queue = queue;
            _auditLogger = auditLogger;
            _unitOfWorkFactory = unitOfWorkFactory;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>"email", " Email " and blank all give "Email"; "sms" gives "SMS"; anything else is refused.</summary>
        public static bool TryNormalizeChannel(string value, out string channel)
        {
            var cleaned = (value ?? string.Empty).Trim();

            if (cleaned.Length == 0 || string.Equals(cleaned, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
            {
                channel = NotificationChannels.Email;
                return true;
            }

            if (string.Equals(cleaned, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase))
            {
                channel = NotificationChannels.Sms;
                return true;
            }

            channel = null;
            return false;
        }

        /// <param name="f20Identifier">Already normalised (PackageIdentifier.TryNormalize).</param>
        /// <param name="channel">Already normalised (TryNormalizeChannel).</param>
        public ResendResult Resend(string f20Identifier, string channel, int requestedByUserId)
        {
            var package = _packages.GetByF20Identifier(f20Identifier);
            if (package == null)
            {
                return new ResendResult { Outcome = ResendOutcome.PackageNotFound, Channel = channel };
            }

            using (var unitOfWork = _unitOfWorkFactory.Begin())
            {
                var latest = _queue.GetLatestForPackage(package.PackageId, channel, unitOfWork);
                if (latest == null)
                {
                    return new ResendResult { Outcome = ResendOutcome.NoNotification, Channel = channel };
                }

                if (!string.Equals(latest.Status, "Failed", StringComparison.OrdinalIgnoreCase)
                    || !_queue.Requeue(latest.NotificationQueueId, unitOfWork))
                {
                    return new ResendResult
                    {
                        Outcome = ResendOutcome.NotFailed,
                        NotificationQueueId = latest.NotificationQueueId,
                        Channel = channel,
                        TemplateKey = latest.TemplateKey,
                        CurrentStatus = latest.Status
                    };
                }

                // Channel and template only: no address or name in the audit log (SR-03)
                _auditLogger.Log(
                    AuditActions.NotificationResent,
                    AuditEntityTypes.Package,
                    package.F20Identifier,
                    channel + " " + latest.TemplateKey + " notification re-queued (queue item " + latest.NotificationQueueId + ")",
                    requestedByUserId,
                    unitOfWork);

                unitOfWork.Commit();

                return new ResendResult
                {
                    Outcome = ResendOutcome.Requeued,
                    NotificationQueueId = latest.NotificationQueueId,
                    Channel = channel,
                    TemplateKey = latest.TemplateKey,
                    QueuedAtUtc = _utcNow()
                };
            }
        }
    }
}