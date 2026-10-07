using System;

namespace CourierService.Domain.Entities
{
    // What the audit log viewer can filter on. Anything left null is not filtered.
    public class AuditLogFilter
    {
        public string Username { get; set; }
        public string Action { get; set; }

        // Start of the range, included
        public DateTime? FromUtc { get; set; }

        // End of the range, NOT included. For "up to 5 Oct" pass 6 Oct 00:00.
        public DateTime? ToUtc { get; set; }
    }
}