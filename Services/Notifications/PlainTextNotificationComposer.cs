using System;
using System.Globalization;
using System.Text;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Composes notifications using subjects and email body templates
    /// stored in dbo.AppConfig.
    /// </summary>
    public class PlainTextNotificationComposer : INotificationComposer
    {
        public const string ServiceName = "F20 Courier Service";

        private readonly IAppConfigRepository _config;

        public PlainTextNotificationComposer(IAppConfigRepository config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _config = config;
        }

        public NotificationMessage Compose(
            NotificationQueueItem item,
            Package package)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (package == null)
                throw new ArgumentNullException(nameof(package));

            var recipient = package.Recipient ?? new Recipient();

            var isSms = string.Equals(
                item.Channel,
                NotificationChannels.Sms,
                StringComparison.OrdinalIgnoreCase);

            return new NotificationMessage
            {
                NotificationQueueId = item.NotificationQueueId,
                PackageId = package.PackageId,
                F20Identifier = package.F20Identifier,
                Channel = item.Channel,
                TemplateKey = item.TemplateKey,

                To = ((isSms
                    ? recipient.PhoneNumber
                    : recipient.Email) ?? string.Empty).Trim(),

                Subject = SubjectFor(item.TemplateKey),

                Body = isSms
                    ? SmsBody(item.TemplateKey, package)
                    : EmailBody(item.TemplateKey, package, recipient)
            };
        }

        public string SubjectFor(string templateKey)
        {
            var configured = _config.GetValue(
                "Notification." + templateKey + ".Subject");

            if (!string.IsNullOrWhiteSpace(configured))
                return configured.Trim();

            return templateKey == NotificationTemplateKeys.Collected
                ? "Your package has been collected"
                : "Your package is ready for collection";
        }

        private string EmailBody(
            string templateKey,
            Package package,
            Recipient recipient)
        {
            var isCollected =
                templateKey == NotificationTemplateKeys.Collected;

            var configured = _config.GetValue(
                "Notification." + templateKey + ".Body");

            var template = !string.IsNullOrWhiteSpace(configured)
                ? configured
                : isCollected
                    ? "Hello {{RecipientName}}, your package {{PackageId}} was collected on {{CollectionTime}}. If you did not collect it, please contact the courier office. F20 Courier Service"
                    : "Hello {{RecipientName}}, your package {{PackageId}} is ready for collection at {{StorageLocation}}. Please bring your ID. F20 Courier Service";

            return template
                .Replace(
                    "{{RecipientName}}",
                    string.IsNullOrWhiteSpace(recipient.FullName)
                        ? "there"
                        : recipient.FullName.Trim())
                .Replace(
                    "{{PackageId}}",
                    package.F20Identifier ?? string.Empty)
                .Replace(
                    "{{StorageLocation}}",
                    string.IsNullOrWhiteSpace(package.StorageLocationCode)
                        ? "the F20 collection area"
                        : package.StorageLocationCode.Trim())
                .Replace(
                    "{{CollectionTime}}",
                    FormatCollectionTime(package));
        }

        private static string SmsBody(
            string templateKey,
            Package package)
        {
            // Keep SMS messages short and suitable for the existing SMS workflow.
            return templateKey == NotificationTemplateKeys.Collected
                ? ServiceName + ": package " + package.F20Identifier
                    + " was collected" + CollectedOn(package) + "."
                : ServiceName + ": package " + package.F20Identifier
                    + " is ready for collection" + Where(package)
                    + ". Bring your ID.";
        }

        private static string Where(Package package)
        {
            return string.IsNullOrWhiteSpace(package.StorageLocationCode)
                ? string.Empty
                : " at " + package.StorageLocationCode.Trim();
        }

        private static string CollectedOn(Package package)
        {
            return package.CollectedAtUtc.HasValue
                ? " on " + package.CollectedAtUtc.Value.ToString(
                    "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture) + " (UTC)"
                : string.Empty;
        }

        private static string FormatCollectionTime(Package package)
        {
            return package.CollectedAtUtc.HasValue
                ? package.CollectedAtUtc.Value.ToString(
                    "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture) + " (UTC)"
                : "not recorded";
        }
    }
}