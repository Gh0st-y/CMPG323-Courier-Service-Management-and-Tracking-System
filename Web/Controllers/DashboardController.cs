using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Repositories;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// T23: dashboard counts, per the API contract, plus a recent-activity list. The list's
    /// shape is a placeholder — not yet defined in docs/API_CONTRACT.md (see PR notes).
    /// </summary>
    // TODO: add [Authorize] once login (FR-01) lands.
    public class DashboardController : Controller
    {
        private readonly IDashboardRepository _dashboard;

        public DashboardController()
            : this(new DashboardRepository(new SqlConnectionFactory()))
        {
        }

        public DashboardController(IDashboardRepository dashboard)
        {
            _dashboard = dashboard;
        }

        [HttpGet]
        [Route("api/dashboard/stats")]
        public ActionResult Stats(string period)
        {
            var allowedPeriods = new[] { "today", "week", "month" };
            var normalizedPeriod = string.IsNullOrWhiteSpace(period) ? "today" : period.Trim().ToLowerInvariant();

            if (!allowedPeriods.Contains(normalizedPeriod))
            {
                Response.StatusCode = 400;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { error = new { code = "InvalidPeriod", message = "period must be one of: today, week, month." } },
                    JsonRequestBehavior.AllowGet);
            }

            try
            {
                var stats = _dashboard.GetStats(normalizedPeriod);
                var activity = _dashboard.GetRecentActivity(10)
                    .Select(a => new
                    {
                        f20Identifier = a.F20Identifier,
                        fromStatus = a.FromStatus,
                        toStatus = a.ToStatus,
                        changedBy = a.ChangedByUsername,
                        changedAtUtc = a.ChangedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                    })
                    .ToList();

                return Json(new
                {
                    period = normalizedPeriod,
                    receivedToday = stats.ReceivedToday,
                    readyForCollection = stats.ReadyForCollection,
                    collectedToday = stats.CollectedToday,
                    outstanding = stats.Outstanding,
                    recentActivity = activity
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { error = new { code = "ServerError", message = "Something went wrong while loading the dashboard. Please try again." } },
                    JsonRequestBehavior.AllowGet);
            }
        }
    }
}