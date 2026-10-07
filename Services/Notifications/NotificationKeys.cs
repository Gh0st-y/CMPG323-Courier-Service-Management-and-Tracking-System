namespace CourierService.Services.Notifications
{
    /// <summary>Values stored in dbo.NotificationQueue.Channel (schema.sql: 'Email' or 'SMS').</summary>
    public static class NotificationChannels
    {
        public const string Email = "Email";
        public const string Sms = "SMS";
    }

    /// <summary>
    /// Values stored in dbo.NotificationQueue.TemplateKey, one per status change that tells the recipient something
    /// (FR-06). They also name the subject settings in dbo.AppConfig, e.g. Notification.ReadyForCollection.Subject.
    /// </summary>
    public static class NotificationTemplateKeys
    {
        public const string ReadyForCollection = "ReadyForCollection";
        public const string Collected = "Collected";
    }
}