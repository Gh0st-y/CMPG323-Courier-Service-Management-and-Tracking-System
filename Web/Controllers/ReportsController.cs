using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;

namespace CourierService.Web.Controllers
{
    /// <summary>Reports dashboard for Supervisors and System Admins (SR-02, FR-15).</summary>
    public class ReportsController : Controller
    {
        private static readonly string[] Statuses = { "Registered", "InStorage", "ReadyForCollection", "Collected" };
        private static readonly string[] Classifications = { "Personal", "WorkRelated" };
        private static readonly string[] PaymentStatuses = { "Paid", "Unpaid", "Exempt" };

        private readonly IReportsRepository _reports;

        public ReportsController()
            : this(new ReportsRepository(new SqlConnectionFactory()))
        {
        }

        public ReportsController(IReportsRepository reports)
        {
            _reports = reports;
        }

        [RoleAuthorize(RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [RoleAuthorize(RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpGet]
        [Route("api/reports")]
        public ActionResult Data(DateTime? from, DateTime? to, string status, string classification, string paymentStatus)
        {
            if (!IsAllowed(status, Statuses))
                return BadRequest("InvalidStatus", "status must be one of: " + string.Join(", ", Statuses) + ".");
            if (!IsAllowed(classification, Classifications))
                return BadRequest("InvalidClassification", "classification must be one of: " + string.Join(", ", Classifications) + ".");
            if (!IsAllowed(paymentStatus, PaymentStatuses))
                return BadRequest("InvalidPaymentStatus", "paymentStatus must be one of: " + string.Join(", ", PaymentStatuses) + ".");
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
                return BadRequest("InvalidDateRange", "from must not be after to.");

            var report = _reports.GetReport(new ReportFilter
            {
                FromDate = from,
                ToDate = to,
                Status = status,
                Classification = classification,
                PaymentStatus = paymentStatus
            });

            return Json(new
            {
                kpis = new
                {
                    total = report.Kpis.Total,
                    readyForCollection = report.Kpis.ReadyForCollection,
                    collected = report.Kpis.Collected,
                    averageDaysToCollect = report.Kpis.AverageDaysToCollect,
                    feesPaid = report.Kpis.FeesPaid,
                    feesUnpaid = report.Kpis.FeesUnpaid
                },
                perDay = report.PerDay.Select(p => new { label = p.Label, value = p.Value }),
                byStatus = report.ByStatus.Select(p => new { label = p.Label, value = p.Value }),
                byClassification = report.ByClassification.Select(p => new { label = p.Label, value = p.Value }),
                byPaymentStatus = report.ByPaymentStatus.Select(p => new { label = p.Label, value = p.Value }),
                outstanding = report.Outstanding.Select(o => new
                {
                    f20Identifier = o.F20Identifier,
                    recipientName = o.RecipientName,
                    status = o.Status,
                    storageLocation = o.StorageLocation,
                    ageDays = o.AgeDays
                })
            }, JsonRequestBehavior.AllowGet);
        }

        private static bool IsAllowed(string value, string[] allowed)
        {
            return string.IsNullOrEmpty(value) || allowed.Contains(value);
        }

        private ActionResult BadRequest(string code, string message)
        {
            Response.StatusCode = 400;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } }, JsonRequestBehavior.AllowGet);
        }

        // Same as DashboardController: log, then let the global filter (T52) choose 503 vs 500.
        protected override void OnException(ExceptionContext filterContext)
        {
            Trace.TraceError(filterContext.Exception.ToString());
            base.OnException(filterContext);
        }
    }
}