namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Sends one notification over one channel. The email sender (T26, SMTP) and the SMS adapter (T29) implement this.
    /// Report problems through the result rather than throwing; if a sender does throw, the worker counts it as a
    /// failed attempt.
    /// </summary>
    public interface INotificationSender
    {
        /// <summary>The channel this sender handles: NotificationChannels.Email or NotificationChannels.Sms.</summary>
        string Channel { get; }

        NotificationSendResult Send(NotificationMessage message);
    }
}