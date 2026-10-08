using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    public interface INotificationRepository
    {
        void Enqueue(NotificationQueueItem item, IUnitOfWork unitOfWork = null);

        /// <summary>Oldest pending item first, for the background worker to pick up. Null if the queue is empty.</summary>
        NotificationQueueItem GetNextPending();

        void MarkSent(int notificationQueueId, IUnitOfWork unitOfWork = null);

        void MarkFailed(int notificationQueueId, IUnitOfWork unitOfWork = null);

        /// <summary>
        /// Oldest pending item that is due: never tried, or last tried at or before <paramref name="retryBeforeUtc"/>
        /// (so a failed attempt waits before the next try). Null if nothing is due (T25).
        /// </summary>
        NotificationQueueItem GetNextDue(System.DateTime retryBeforeUtc, IUnitOfWork unitOfWork = null);

        /// <summary>Counts a failed attempt but keeps the item Pending so the worker tries again later (T25).</summary>
        void RecordFailedAttempt(int notificationQueueId, IUnitOfWork unitOfWork = null);

        /// <summary>The package's most recent notification on this channel, whatever its status. Null if there is none (T49).</summary>
        NotificationQueueItem GetLatestForPackage(int packageId, string channel, IUnitOfWork unitOfWork = null);

        /// <summary>
        /// Puts a Failed item back to Pending with no attempts, so the worker sends it again on its next run (T49).
        /// Returns false if the item wasn't Failed any more (e.g. two people pressed Resend at once).
        /// </summary>
        bool Requeue(int notificationQueueId, IUnitOfWork unitOfWork = null);
    }
}