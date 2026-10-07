using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CourierService.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Dashboard
{
    [TestClass]
    public class DashboardPeriodWindowTests
    {
        // South African time: UTC+2 all year, no daylight saving. A custom zone keeps the test independent of the
        // machine's time zone database.
        private static readonly TimeZoneInfo Sast =
            TimeZoneInfo.CreateCustomTimeZone("SAST-test", TimeSpan.FromHours(2), "SAST", "SAST");

        private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0)
        {
            return new DateTime(y, m, d, h, min, 0, DateTimeKind.Utc);
        }

        [TestMethod]
        public void Today_MidDay_StartsAtLocalMidnight_WhichIsTwoHoursBeforeUtcMidnight()
        {
            var window = DashboardPeriodWindow.For("today", Utc(2026, 10, 6, 10, 0), Sast);

            Assert.AreEqual(Utc(2026, 10, 5, 22, 0), window.StartUtc);
            Assert.AreEqual(Utc(2026, 10, 6, 22, 0), window.EndUtc);
        }

        [TestMethod]
        public void Today_ShortlyAfterLocalMidnight_ButStillYesterdayInUtc_IsTheNewLocalDay()
        {
            // 23:30 UTC on the 5th is 01:30 on the 6th in South Africa, so "today" is the 6th.
            var window = DashboardPeriodWindow.For("today", Utc(2026, 10, 5, 23, 30), Sast);

            Assert.AreEqual(Utc(2026, 10, 5, 22, 0), window.StartUtc);
            Assert.AreEqual(Utc(2026, 10, 6, 22, 0), window.EndUtc);
        }

        [TestMethod]
        public void Today_PackageReceivedJustAfterLocalMidnight_FallsInsideTheWindow()
        {
            var window = DashboardPeriodWindow.For("today", Utc(2026, 10, 5, 23, 30), Sast);
            var receivedAt = Utc(2026, 10, 5, 22, 30); // 00:30 local on the 6th

            Assert.IsTrue(receivedAt >= window.StartUtc && receivedAt < window.EndUtc);
        }

        [TestMethod]
        public void Today_LastMomentBeforeLocalMidnight_StillBelongsToThatDay_AndMidnightStartsTheNext()
        {
            var window = DashboardPeriodWindow.For("today", Utc(2026, 10, 6, 21, 59), Sast);

            Assert.AreEqual(Utc(2026, 10, 5, 22, 0), window.StartUtc);
            Assert.AreEqual(Utc(2026, 10, 6, 22, 0), window.EndUtc);

            var next = DashboardPeriodWindow.For("today", Utc(2026, 10, 6, 22, 0), Sast);
            Assert.AreEqual(Utc(2026, 10, 6, 22, 0), next.StartUtc);
        }

        [TestMethod]
        public void Week_IsTheLastSevenLocalDaysIncludingToday()
        {
            var window = DashboardPeriodWindow.For("week", Utc(2026, 10, 6, 10, 0), Sast);

            Assert.AreEqual(Utc(2026, 9, 29, 22, 0), window.StartUtc); // local 30 Sep 00:00
            Assert.AreEqual(Utc(2026, 10, 6, 22, 0), window.EndUtc);
            Assert.AreEqual(TimeSpan.FromDays(7), window.EndUtc - window.StartUtc);
        }

        [TestMethod]
        public void Month_IsTheLastThirtyLocalDaysIncludingToday()
        {
            var window = DashboardPeriodWindow.For("month", Utc(2026, 10, 6, 10, 0), Sast);

            Assert.AreEqual(Utc(2026, 9, 6, 22, 0), window.StartUtc); // local 7 Sep 00:00
            Assert.AreEqual(Utc(2026, 10, 6, 22, 0), window.EndUtc);
            Assert.AreEqual(TimeSpan.FromDays(30), window.EndUtc - window.StartUtc);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("today")]
        [DataRow("TODAY")]
        [DataRow("something-else")]
        public void MissingOrUnknownPeriod_IsTreatedAsToday(string period)
        {
            var window = DashboardPeriodWindow.For(period, Utc(2026, 10, 6, 10, 0), Sast);

            Assert.AreEqual(TimeSpan.FromDays(1), window.EndUtc - window.StartUtc);
        }

        [TestMethod]
        public void PeriodNameIsCaseInsensitive()
        {
            var window = DashboardPeriodWindow.For(" Week ", Utc(2026, 10, 6, 10, 0), Sast);

            Assert.AreEqual(TimeSpan.FromDays(7), window.EndUtc - window.StartUtc);
        }

        [TestMethod]
        public void NullZone_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => DashboardPeriodWindow.For("today", Utc(2026, 10, 6), null));
        }
    }
}
