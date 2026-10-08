using System;

namespace CourierService.Domain.Entities
{
    /// <summary>
    /// One row of dbo.NotificationLog: a single attempt to send a notification, successful or not (T28, IR-003).
    /// Cross-referenced to the package by PackageId, so the package detail page can list them (DR-012).
    /// </summary>
    public class NotificationLogEntry
    {
        public const string StatusSent = "Sent";
        public const string StatusFailed = "Failed";

        public int PackageId { get; set; }

        /// <summary>'Email' or 'SMS'.</summary>
        public string Channel { get; set; }

        /// <summary>
        /// Email address or phone number it went (or tried to go) to; empty when the recipient had none. Personal
        /// data: stored here as the schema intends, but masked on screen and never written to logs (SR-03).
        /// </summary>
        public string RecipientAddress { get; set; }

        public string Subject { get; set; }

        /// <summary>StatusSent or StatusFailed.</summary>
        public string Status { get; set; }

        /// <summary>Why it failed, from the sender or worker. Never contains the address or name (SR-03).</summary>
        public string ErrorDetail { get; set; }

        /// <summary>Filled in by the database when the row is added.</summary>
        public DateTime SentAtUtc { get; set; }
    }
}