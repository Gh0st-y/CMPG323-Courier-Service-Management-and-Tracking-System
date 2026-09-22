using System;

namespace CourierService.Domain.Entities
{
    /// <summary>Maps to dbo.Packages. Recipient is populated by repository methods that join it.</summary>
    public class Package
    {
        public int PackageId { get; set; }
        public string F20Identifier { get; set; }
        public int RecipientId { get; set; }
        public Recipient Recipient { get; set; }
        public string SenderName { get; set; }
        public string PackageType { get; set; }
        public string Classification { get; set; }
        public decimal Fee { get; set; }
        public string PaymentStatus { get; set; }
        public PackageStatus Status { get; set; }
        public int? StorageLocationId { get; set; }
        public string Notes { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CollectedAtUtc { get; set; }
        public int? CollectedByUserId { get; set; }
        public byte[] RowVersion { get; set; }
    }
}
