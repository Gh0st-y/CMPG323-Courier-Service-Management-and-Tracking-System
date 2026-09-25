using System;

namespace CourierService.Domain.Models
{
    /// <summary>
    /// Placeholder shape for T23's "recent activity" requirement — not defined in the API
    /// contract yet. Adjust once the leader confirms the expected fields (see PR notes).
    /// </summary>
    public class RecentActivityItem
    {
        public string F20Identifier { get; set; }
        public string FromStatus { get; set; }
        public string ToStatus { get; set; }
        public string ChangedByUsername { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }
}