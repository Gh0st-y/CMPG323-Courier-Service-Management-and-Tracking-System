using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Entities;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;

namespace CourierService.Web.Controllers
{
    /// <summary>T21: GET /api/packages, filtered and paged package search.</summary>
    // TODO: add [Authorize] once login (FR-01) lands. The contract says "any authenticated".
    public class PackagesController : Controller
    {
        private readonly IPackageRepository _packages;

        // MVC needs a parameterless constructor because there is no DI container yet.
        public PackagesController()
            : this(new PackageRepository(new SqlConnectionFactory()))
        {
        }

        public PackagesController(IPackageRepository packages)
        {
            _packages = packages;
        }

        [HttpGet]
        [Route("api/packages")]
        public ActionResult Search(string query, string status, string dateFrom, string dateTo, int? page, int? pageSize)
        {
            PackageStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                PackageStatus s;
                if (!Enum.TryParse(status.Trim(), true, out s) || !Enum.IsDefined(typeof(PackageStatus), s))
                {
                    return ErrorJson(400, "InvalidStatus",
                        "Status must be one of: " + string.Join(", ", Enum.GetNames(typeof(PackageStatus))) + ".");
                }
                parsedStatus = s;
            }

            DateTime? from = null;
            if (!string.IsNullOrWhiteSpace(dateFrom))
            {
                DateTime d;
                if (!DateTime.TryParseExact(dateFrom.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    return ErrorJson(400, "InvalidDate", "dateFrom must look like 2026-09-24.");
                from = d;
            }

            DateTime? to = null;
            if (!string.IsNullOrWhiteSpace(dateTo))
            {
                DateTime d;
                if (!DateTime.TryParseExact(dateTo.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    return ErrorJson(400, "InvalidDate", "dateTo must look like 2026-09-24.");
                to = d;
            }

            if (from.HasValue && to.HasValue && from.Value > to.Value)
                return ErrorJson(400, "InvalidDateRange", "dateFrom must not be after dateTo.");

            var criteria = new PackageSearchCriteria
            {
                Query = query,
                ReceivedFrom = from,
                ReceivedTo = to,
                Status = parsedStatus,
                Page = page ?? 1,
                PageSize = pageSize ?? 20
            };

            try
            {
                var result = _packages.Search(criteria);

                var items = result.Items.Select(p => new
                {
                    packageId = p.PackageId,
                    f20Identifier = p.F20Identifier,
                    status = p.Status.ToString(),
                    classification = p.Classification,
                    packageType = p.PackageType,
                    paymentStatus = p.PaymentStatus,
                    fee = p.Fee,
                    storageLocationId = p.StorageLocationId,
                    receivedAtUtc = IsoUtc(p.CreatedAtUtc),
                    collectedAtUtc = IsoUtc(p.CollectedAtUtc),
                    recipientName = p.Recipient.FullName,
                    recipientIdentifierNo = p.Recipient.IdentifierNo
                }).ToList();

                return Json(new { items, totalCount = result.TotalCount }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Full detail goes to the debug output only. Callers never see stack traces or SQL (SR-03, OR-04).
                Trace.TraceError(ex.ToString());
                return ErrorJson(500, "ServerError", "Something went wrong while searching. Please try again.");
            }
        }

        private ActionResult ErrorJson(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } }, JsonRequestBehavior.AllowGet);
        }

        private static string IsoUtc(DateTime? value)
        {
            return value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                    .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                : null;
        }
    }
}