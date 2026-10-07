using System.Collections.Generic;

namespace CourierService.Domain.Entities
{
    // One page of audit entries plus the total number of matches across all pages.
    public class AuditLogPage
    {
        public IReadOnlyList<AuditLogEntry> Items { get; set; }
        public int TotalCount { get; set; }

        // Filled in by the service with the values it actually used
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}