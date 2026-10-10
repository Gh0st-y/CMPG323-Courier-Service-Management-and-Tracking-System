using System;

namespace CourierService.Domain.Models
{
    /// <summary>Filters for the Reports page. Dates are local (SAST) calendar days, both inclusive. Null means "not filtered".</summary>
    public class ReportFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Status { get; set; }
        public string Classification { get; set; }
        public string PaymentStatus { get; set; }
    }
}