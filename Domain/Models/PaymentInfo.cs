using System;

namespace CourierService.Domain.Models
{
    /// <summary>A package's payment status and who last changed it, and when (T41, FR-17).</summary>
    public class PaymentInfo
    {
        public int PackageId { get; set; }

        /// <summary>Paid, Unpaid or Exempt.</summary>
        public string PaymentStatus { get; set; }

        /// <summary>Null until staff change it; the status set at registration has no "updated" time.</summary>
        public DateTime? UpdatedAtUtc { get; set; }

        /// <summary>Username of the staff member who last changed it, or null.</summary>
        public string UpdatedBy { get; set; }
    }
}
