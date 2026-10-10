using System.Collections.Generic;

namespace CourierService.Domain.Models
{
    public class ReportData
    {
        public ReportKpis Kpis { get; set; }
        public List<ReportPoint> PerDay { get; set; }
        public List<ReportPoint> ByStatus { get; set; }
        public List<ReportPoint> ByClassification { get; set; }
        public List<ReportPoint> ByPaymentStatus { get; set; }
        public List<OutstandingPackageRow> Outstanding { get; set; }
    }

    public class ReportKpis
    {
        public int Total { get; set; }
        public int ReadyForCollection { get; set; }
        public int Collected { get; set; }
        /// <summary>Average days from registration to collection. Null when nothing in the range was collected.</summary>
        public double? AverageDaysToCollect { get; set; }
        public decimal FeesPaid { get; set; }
        public decimal FeesUnpaid { get; set; }
    }

    /// <summary>One label/value pair for a chart (a day, a status, a fee total, ...).</summary>
    public class ReportPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }

    public class OutstandingPackageRow
    {
        public string F20Identifier { get; set; }
        public string RecipientName { get; set; }
        public string Status { get; set; }
        public string StorageLocation { get; set; }
        public int AgeDays { get; set; }
    }
}