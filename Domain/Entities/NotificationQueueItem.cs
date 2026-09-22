using System;

namespace CourierService.Domain.Entities
{
    /// <summary>Maps to dbo.NotificationQueue — consumed by the async background worker (T-notifications).</summary>
    public class NotificationQueueItem
    {
        public int NotificationQueueId { get; set; }
        public int PackageId { get; set; }
        public string Channel { get; set; }
        public string TemplateKey { get; set; }
        public string Status { get; set; }
        public int AttemptCount { get; set; }
        public DateTime EnqueuedAtUtc { get; set; }
        public DateTime? LastAttemptAtUtc { get; set; }
    }
}
