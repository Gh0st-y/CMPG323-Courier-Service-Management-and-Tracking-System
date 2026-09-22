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
    }
}
