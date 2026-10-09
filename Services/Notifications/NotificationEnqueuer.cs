using System;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Packages;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Queues the recipient's notification when a package becomes Ready for Collection or is Collected (T25, FR-06).
    /// It runs inside the status change's transaction and only adds a queue row, so it takes milliseconds and never
    /// waits for an email server. Sending happens later in the background worker, which is why a broken or switched
    /// off SMTP server can't block or fail the staff member's action (NFR-022).
    /// </summary>
    public class NotificationEnqueuer : IPackageStatusChangeListener
    {
        private readonly INotificationRepository _queue;
        private readonly bool _smsEnabled;

        /// <param name="smsEnabled">Also queue an SMS. Off until the SMS adapter (T29) exists.</param>
        public NotificationEnqueuer(INotificationRepository queue, bool smsEnabled = false)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));

            _queue = queue;
            _smsEnabled = smsEnabled;
        }

        public void OnStatusChanged(PackageStatusChange change, IUnitOfWork unitOfWork)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));

            var templateKey = TemplateFor(change.ToStatus);
            if (templateKey == null)
            {
                // Registered and In Storage aren't worth an email
                return;
            }

            Enqueue(change.PackageId, NotificationChannels.Email, templateKey, unitOfWork);

            if (_smsEnabled)
            {
                Enqueue(change.PackageId, NotificationChannels.Sms, templateKey, unitOfWork);
            }
        }

        /// <summary>The template for a new status, or null if that status sends nothing.</summary>
        public static string TemplateFor(PackageStatus status)
        {
            switch (status)
            {
                case PackageStatus.ReadyForCollection:
                    return NotificationTemplateKeys.ReadyForCollection;
                case PackageStatus.Collected:
                    return NotificationTemplateKeys.Collected;
                default:
                    return null;
            }
        }

        private void Enqueue(int packageId, string channel, string templateKey, IUnitOfWork unitOfWork)
        {
            // The same unit of work as the status change: if the change rolls back, so does the notification
            _queue.Enqueue(new NotificationQueueItem
            {
                PackageId = packageId,
                Channel = channel,
                TemplateKey = templateKey
            }, unitOfWork);
        }
    }
}