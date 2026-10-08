using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    /// <summary>Writes dbo.NotificationLog (T28, IR-003). Reading for the detail page is IPackageDetailRepository (T22).</summary>
    public interface INotificationLogRepository
    {
        /// <summary>Adds one attempt. Text longer than its column is cut to fit, so a long error can't make the insert fail.</summary>
        void Add(NotificationLogEntry entry, IUnitOfWork unitOfWork = null);
    }
}