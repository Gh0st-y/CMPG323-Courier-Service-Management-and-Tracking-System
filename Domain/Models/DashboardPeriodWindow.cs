using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourierService.Domain.Models
{
    /// <summary>
    /// The UTC time range a dashboard period covers (T23). Day boundaries are local midnights in the given
    /// time zone, converted to UTC, so "today" is the calendar day staff actually see on the wall rather than
    /// the UTC day. The timestamps in the database are stored in UTC.
    ///   today = the current local day. week = the last 7 local days including today.
    ///   month = the last 30 local days including today. Anything else is treated as today.
    /// </summary>
    public sealed class DashboardPeriodWindow
    {
        private DashboardPeriodWindow(DateTime startUtc, DateTime endUtc)
        {
            StartUtc = startUtc;
            EndUtc = endUtc;
        }

        /// <summary>Start of the period (inclusive), in UTC.</summary>
        public DateTime StartUtc { get; private set; }

        /// <summary>End of the period (exclusive), in UTC: the start of tomorrow, local time.</summary>
        public DateTime EndUtc { get; private set; }

        public static DashboardPeriodWindow For(string period, DateTime utcNow, TimeZoneInfo zone)
        {
            if (zone == null)
            {
                throw new ArgumentNullException("zone");
            }

            int days;
            switch ((period ?? "today").Trim().ToLowerInvariant())
            {
                case "week": days = 7; break;
                case "month": days = 30; break;
                default: days = 1; break;
            }

            var nowUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            var localToday = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone).Date;

            var startLocal = DateTime.SpecifyKind(localToday.AddDays(-(days - 1)), DateTimeKind.Unspecified);
            var endLocal = DateTime.SpecifyKind(localToday.AddDays(1), DateTimeKind.Unspecified);

            return new DashboardPeriodWindow(
                TimeZoneInfo.ConvertTimeToUtc(startLocal, zone),
                TimeZoneInfo.ConvertTimeToUtc(endLocal, zone));
        }
    }
}