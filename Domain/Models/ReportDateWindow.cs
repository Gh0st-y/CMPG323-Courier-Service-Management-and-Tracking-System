using System;
namespace CourierService.Domain.Models
{
    /// <summary>Inclusive South African calendar days, represented as an exclusive UTC range.</summary>
    public class ReportDateWindow
    {
        public DateTime FromLocal { get; private set; }
        public DateTime ToLocal { get; private set; }
        public DateTime FromUtc { get; private set; }
        public DateTime ToUtcExclusive { get; private set; }
        public static ReportDateWindow For(DateTime? from, DateTime? to, DateTime utcNow)
        {
            var today = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)).Date;
            var end = (to ?? today).Date;
            if (end < DateTime.MinValue.AddDays(29) || end == DateTime.MaxValue.Date)
                throw new ArgumentException("The report end date is outside the supported range.");
            var start = (from ?? end.AddDays(-29)).Date;
            if (start < DateTime.MinValue.AddDays(1) || start > end)
                throw new ArgumentException("The report start date must be on or before the end date and within the supported range.");
            return new ReportDateWindow
            {
                FromLocal = start, ToLocal = end,
                FromUtc = new DateTimeOffset(DateTime.SpecifyKind(start, DateTimeKind.Unspecified), TimeSpan.FromHours(2)).UtcDateTime,
                ToUtcExclusive = new DateTimeOffset(DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Unspecified), TimeSpan.FromHours(2)).UtcDateTime
            };
        }
    }
}
