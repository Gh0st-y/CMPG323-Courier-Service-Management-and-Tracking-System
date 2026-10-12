using System;
using CourierService.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CourierService.Tests.Reports
{
    [TestClass]
    public class ReportDateWindowTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 10, 22, 30, 0, DateTimeKind.Utc);
        [TestMethod]
        public void DefaultsToThirtySouthAfricanCalendarDays()
        {
            var window = ReportDateWindow.For(null, null, Now);
            Assert.AreEqual(new DateTime(2026, 10, 11), window.ToLocal);
            Assert.AreEqual(new DateTime(2026, 9, 12), window.FromLocal);
            Assert.AreEqual(new DateTime(2026, 9, 11, 22, 0, 0, DateTimeKind.Utc), window.FromUtc);
            Assert.AreEqual(new DateTime(2026, 10, 11, 22, 0, 0, DateTimeKind.Utc), window.ToUtcExclusive);
        }
        [TestMethod]
        public void HistoricalEndDateDefaultsTheStartRelativeToThatEnd()
        {
            var window = ReportDateWindow.For(null, new DateTime(2020, 2, 29), Now);
            Assert.AreEqual(new DateTime(2020, 1, 31), window.FromLocal);
        }
        [TestMethod]
        public void RejectsReversedAndUnrepresentableRanges()
        {
            Assert.ThrowsExactly<ArgumentException>(() => ReportDateWindow.For(new DateTime(2026, 10, 12), null, Now));
            Assert.ThrowsExactly<ArgumentException>(() => ReportDateWindow.For(DateTime.MinValue, new DateTime(2026, 10, 10), Now));
            Assert.ThrowsExactly<ArgumentException>(() => ReportDateWindow.For(null, DateTime.MaxValue, Now));
        }
    }
}
