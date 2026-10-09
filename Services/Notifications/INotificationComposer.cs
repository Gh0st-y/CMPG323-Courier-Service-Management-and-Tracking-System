using CourierService.Domain.Entities;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Turns a queue item and its package into the message to send: who it goes to, the subject and the text.
    /// The templates task (T27) replaces PlainTextNotificationComposer with proper templates behind this interface.
    /// </summary>
    public interface INotificationComposer
    {
        /// <param name="package">The package, loaded with its recipient and storage location code.</param>
        NotificationMessage Compose(NotificationQueueItem item, Package package);
    }
}