using System.Diagnostics;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Development stand-in until the SMTP sender (T26) is in: instead of sending, it writes one line to the debug
    /// output (Visual Studio's Output window) and reports success. It deliberately leaves out the address (SR-03).
    /// </summary>
    public class TraceNotificationSender : INotificationSender
    {
        private readonly string _channel;

        public TraceNotificationSender(string channel = NotificationChannels.Email)
        {
            _channel = channel;
        }

        public string Channel => _channel;

        public NotificationSendResult Send(NotificationMessage message)
        {
            Trace.TraceInformation(
                "Notification {0}: {1} for {2} by {3} (not really sent: no sender configured yet, T26).",
                message.NotificationQueueId, message.TemplateKey, message.F20Identifier, message.Channel);

            return NotificationSendResult.Sent();
        }
    }
}