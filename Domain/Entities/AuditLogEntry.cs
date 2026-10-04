using System;

namespace CourierService.Domain.Entities
{
    /// <summary>
    /// Maps to dbo.AuditLog (SR-04, NFR-019). Append-only — repositories must only ever
    /// INSERT rows here, never UPDATE or DELETE. Detail must not contain personal data (SR-03).
    /// </summary>
    public class AuditLogEntry
    {
        public long AuditLogId { get; set; }
        public int? UserId { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Detail { get; set; }
        // Not a column in AuditLog. Only filled in by the viewer search, from the Users table.
        public string Username { get; set; }
        public DateTime OccurredAtUtc { get; set; }
    }
}
