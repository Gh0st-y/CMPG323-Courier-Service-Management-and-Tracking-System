
using System.Diagnostics;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Development SMS adapter. Logs a safe message to the debug output
    /// instead of sending a real SMS.
    /// </summary>
    public class StubSmsNotificationSender : INotificationSender
    {
        public string Channel => NotificationChannels.Sms;

        public NotificationSendResult Send(NotificationMessage message)
        {
            if (message == null)
            {
                return NotificationSendResult.Failed(
                    "SMS notification message is missing.",
                    permanent: true);
            }

            Trace.TraceInformation(
                "SMS stub: Notification {0} for package {1} processed by the SMS stub (not really sent).",
                message.NotificationQueueId,
                message.F20Identifier);

            return NotificationSendResult.Sent();
        }
    }
}
