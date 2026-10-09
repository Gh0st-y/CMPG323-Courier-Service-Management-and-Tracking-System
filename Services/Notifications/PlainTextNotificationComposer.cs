using System;
using System.Globalization;
using System.Text;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Simple plain-text messages so the queue and worker (T25) can run end to end before the templates (T27) exist.
    /// Email subjects come from dbo.AppConfig (Notification.{TemplateKey}.Subject, OR-01), with a built-in fallback.
    /// Emails go to the recipient's email address and SMS to their phone number; if that is missing, To is empty
    /// and the worker records a permanent failure instead of sending.
    /// </summary>
    public class PlainTextNotificationComposer : INotificationComposer
    {
        public const string ServiceName = "F20 Courier Service";

        private readonly IAppConfigRepository _config;

        public PlainTextNotificationComposer(IAppConfigRepository config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            _config = config;
        }

        public NotificationMessage Compose(NotificationQueueItem item, Package package)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (package == null) throw new ArgumentNullException(nameof(package));

            var recipient = package.Recipient ?? new Recipient();
            var isSms = string.Equals(item.Channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase);

            return new NotificationMessage
            {
                NotificationQueueId = item.NotificationQueueId,
                PackageId = package.PackageId,
                F20Identifier = package.F20Identifier,
                Channel = item.Channel,
                TemplateKey = item.TemplateKey,
                To = ((isSms ? recipient.PhoneNumber : recipient.Email) ?? string.Empty).Trim(),
                Subject = SubjectFor(item.TemplateKey),
                Body = isSms ? SmsBody(item.TemplateKey, package) : EmailBody(item.TemplateKey, package, recipient)
            };
        }

        public string SubjectFor(string templateKey)
        {
            var configured = _config.GetValue("Notification." + templateKey + ".Subject");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim();
            }

            // Same text as the seeded AppConfig rows (db/schema.sql), in case a row was removed
            return templateKey == NotificationTemplateKeys.Collected
                ? "Your package has been collected"
                : "Your package is ready for collection";
        }

        private static string EmailBody(string templateKey, Package package, Recipient recipient)
        {
            var text = new StringBuilder();
            text.AppendLine("Hello " + (string.IsNullOrWhiteSpace(recipient.FullName) ? "there" : recipient.FullName.Trim()) + ",");
            text.AppendLine();

            if (templateKey == NotificationTemplateKeys.Collected)
            {
                text.AppendLine("Your package " + package.F20Identifier + " was collected" + CollectedOn(package) + ".");
                text.AppendLine();
                text.AppendLine("If you did not collect it, please contact the courier office.");
            }
            else
            {
                text.AppendLine("Your package " + package.F20Identifier + " is ready for collection" + Where(package) + ".");
                text.AppendLine();
                text.AppendLine("Please bring your ID and quote the package number when you collect it.");
            }

            text.AppendLine();
            text.AppendLine(ServiceName);
            return text.ToString();
        }

        private static string SmsBody(string templateKey, Package package)
        {
            // Kept short for one SMS, and without the name (the phone may be shared)
            return templateKey == NotificationTemplateKeys.Collected
                ? ServiceName + ": package " + package.F20Identifier + " was collected" + CollectedOn(package) + "."
                : ServiceName + ": package " + package.F20Identifier + " is ready for collection" + Where(package) + ". Bring your ID.";
        }

        private static string Where(Package package)
        {
            return string.IsNullOrWhiteSpace(package.StorageLocationCode) ? string.Empty : " at " + package.StorageLocationCode.Trim();
        }

        private static string CollectedOn(Package package)
        {
            // UTC on purpose: the templates task (T27) decides how to show local time
            return package.CollectedAtUtc.HasValue
                ? " on " + package.CollectedAtUtc.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " (UTC)"
                : string.Empty;
        }
    }
}