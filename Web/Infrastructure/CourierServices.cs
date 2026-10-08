using System;
using System.Collections.Generic;
using System.Configuration;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Services.Audit;
using CourierService.Services.Notifications;
using CourierService.Services.Packages;

namespace CourierService.Web.Infrastructure
{
    /// <summary>
    /// Wires the services together from the Data repositories, in one place, so each controller doesn't repeat it.
    /// Controllers also take the interfaces in a second constructor, so a different set can be passed in.
    /// </summary>
    public static class CourierServices
    {
        public static IPackageLookupService Lookup(IDbConnectionFactory connectionFactory)
        {
            return new PackageLookupService(new PackageRepository(connectionFactory));
        }

        public static IPackageStatusService Status(IDbConnectionFactory connectionFactory)
        {
            return new PackageStatusService(
                new PackageRepository(connectionFactory),
                new PackageStatusHistoryRepository(connectionFactory),
                new StorageLocationRepository(connectionFactory),
                new AuditLogger(new AuditLogRepository(connectionFactory)),
                new UnitOfWorkFactory(connectionFactory),
                new IPackageStatusChangeListener[]
                {
                    // T25: Ready for Collection and Collected queue the recipient's notification in the same transaction
                    new NotificationEnqueuer(new NotificationRepository(connectionFactory), SmsEnabled())
                });
        }

        public static IPackageCollectionService Collection(IDbConnectionFactory connectionFactory)
        {
            return new PackageCollectionService(
                Status(connectionFactory),
                new PackageRepository(connectionFactory),
                new UserRepository(connectionFactory));
        }

        /// <summary>The background worker's processor (T25). One is made per run, so each run reads fresh settings. Every attempt is logged (T28).</summary>
        public static NotificationProcessor NotificationProcessor(IDbConnectionFactory connectionFactory, int maxAttempts, TimeSpan retryDelay)
        {
            return new NotificationProcessor(
                new NotificationRepository(connectionFactory),
                new PackageRepository(connectionFactory),
                new PlainTextNotificationComposer(new AppConfigRepository(connectionFactory)),
                NotificationSenders(),
                maxAttempts,
                retryDelay,
                log: new NotificationLogRepository(connectionFactory));
        }

        /// <summary>
        /// One sender per channel. Email goes through SMTP (T26) using the Smtp.* settings in Web.config. With Smtp.Host
        /// left empty, the stand-in writes a line to the Output window instead, for machines without a mail server.
        /// T29: replace the SMS stand-in here.
        /// </summary>
        private static IEnumerable<INotificationSender> NotificationSenders()
        {
            var smtp = SmtpSettings.FromAppSettings(ConfigurationManager.AppSettings);
            yield return string.IsNullOrWhiteSpace(smtp.Host)
                ? (INotificationSender)new TraceNotificationSender(NotificationChannels.Email)
                : new SmtpNotificationSender(smtp);

            if (SmsEnabled())
            {
                yield return new TraceNotificationSender(NotificationChannels.Sms);
            }
        }

        /// <summary>Web.config Sms.Enabled (off by default, DECISIONS.md #6). No SMS is queued while it is off.</summary>
        private static bool SmsEnabled()
        {
            bool enabled;
            return bool.TryParse(ConfigurationManager.AppSettings["Sms.Enabled"], out enabled) && enabled;
        }
    }
}