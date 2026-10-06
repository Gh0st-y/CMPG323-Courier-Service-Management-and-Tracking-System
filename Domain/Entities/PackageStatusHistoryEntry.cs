using System;

namespace CourierService.Domain.Entities
{
    /// <summary>Maps to dbo.PackageStatusHistory (FR-04, DR-009, DR-010). One row per status change, never edited.</summary>
    public class PackageStatusHistoryEntry
    {
        public int PackageStatusHistoryId { get; set; }
        public int PackageId { get; set; }

        /// <summary>Null for the first entry of a package (it has no previous status).</summary>
        public PackageStatus? FromStatus { get; set; }

        public PackageStatus ToStatus { get; set; }
        public int ChangedByUserId { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string Notes { get; set; }
    }
}
