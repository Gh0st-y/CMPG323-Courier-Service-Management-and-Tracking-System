using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Entities;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;
using CourierService.Services.Packages;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using CourierService.Services.Security;

namespace CourierService.Web.Controllers
{
    public partial class PackagesController : Controller
    {
        private readonly IPackageRepository _packages;
        private readonly IPackageDetailRepository _packageDetail;
        private readonly IPackagePaymentRepository _payments;

        public PackagesController()
            : this(new PackageRepository(new SqlConnectionFactory()),
                   new PackageDetailRepository(new SqlConnectionFactory()),
                   new PackagePaymentRepository(new SqlConnectionFactory()))
        {
        }

        public PackagesController(IPackageRepository packages, IPackageDetailRepository packageDetail, IPackagePaymentRepository payments)
        {
            _packages = packages;
            _packageDetail = packageDetail;
            _payments = payments;
        }

        /// <summary>
        /// Renders the package registration form (FR-03, UR-01).
        /// All API calls are made client-side via CourierApp.api, so this action just returns the view.
        /// </summary>
        [RoleAuthorize(RoleNames.IntakeClerk, RoleNames.Supervisor, RoleNames.SystemAdmin)]
        public ActionResult Register()
        {
            return View();
        }

        /// <summary>
        /// Renders the print-friendly label view for a package (FR-03, CON-008).
        /// The f20Identifier is passed through to the view, which fetches details via CourierApp.api.
        /// </summary>
        [RoleAuthorize(RoleNames.IntakeClerk, RoleNames.Supervisor, RoleNames.SystemAdmin)]
        public ActionResult Label(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Register");
            }

            string identifier;
            if (!PackageIdentifier.TryNormalize(id, out identifier))
            {
                return HttpNotFound("No package was found for that code.");
            }

            return View(model: identifier);
        }

        /// <summary>
        /// Renders the search and results page (FR-05, UR-01).
        /// Filtering and paging happen client-side via CourierApp.api against Get/api/packages.
        /// </summary>
        [RoleAuthorize]
        public ActionResult Search()
        {
            return View();
        }

        /// <summary>
        /// Renders the package detail page with status, location, fee, timeline, and notifications (FR-05, DR-012).
        /// The f20Identifier is passed to the view, which fetches details via CourierApp.api.
        /// </summary>
        [RoleAuthorize]
        public ActionResult Detail(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Search");
            }
            string identifier;
            if (!PackageIdentifier.TryNormalize(id, out identifier))
            {
                return HttpNotFound("No package was found for that code.");
            }

            return View(model: identifier);
        }

        /// <summary>T21: GET /api/packages — JSON data for the Search page above. Any logged-in user (SR-02).</summary>
        [RoleAuthorize]
        [HttpGet]
        [Route("api/packages")]
        public ActionResult SearchJson(string query, string status, string dateFrom, string dateTo, int? page, int? pageSize)
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
                Trace.TraceError(ex.ToString());
                return ErrorJson(500, "ServerError", "Something went wrong while searching. Please try again.");
            }
        }

        /// <summary>T22: GET /api/packages/{f20Identifier}/detail — JSON data for the Detail page above. Any logged-in user (SR-02).</summary>
        [RoleAuthorize]
        [HttpGet]
        [Route("api/packages/{f20Identifier}/detail")]
        public ActionResult DetailJson(string f20Identifier)
        {
            string identifier;
            if (!PackageIdentifier.TryNormalize(f20Identifier, out identifier))
                return ErrorJson(400, "ValidationError", "f20Identifier is required.");

            var package = _packages.GetByF20Identifier(identifier);
            if (package == null)
                return ErrorJson(404, "NotFound", "No package found for that identifier.");

            var statusHistory = _packageDetail.GetStatusHistory(package.PackageId)
                .Select(h => new
                {
                    fromStatus = h.FromStatus,
                    toStatus = h.ToStatus,
                    changedBy = h.ChangedByUsername,
                    changedAtUtc = IsoUtc(h.ChangedAtUtc),
                    notes = h.Notes
                }).ToList();

            var notifications = _packageDetail.GetNotificationLog(package.PackageId)
                .Select(n => new
                {
                    channel = n.Channel,
                    recipientAddress = PersonalData.MaskPhone(n.RecipientAddress),
                    subject = n.Subject,
                    status = n.Status,
                    sentAtUtc = IsoUtc(n.SentAtUtc)
                }).ToList();

            var payment = _payments.GetPaymentInfo(package.PackageId);

            var packageJson = new
            {
                packageId = package.PackageId,
                f20Identifier = package.F20Identifier,
                status = package.Status.ToString(),
                classification = package.Classification,
                packageType = package.PackageType,
                paymentStatus = package.PaymentStatus,
                // T41: who last changed the payment status and when; null until staff change it
                paymentStatusUpdatedAtUtc = payment == null ? null : IsoUtc(payment.UpdatedAtUtc),
                paymentStatusUpdatedBy = payment == null ? null : payment.UpdatedBy,
                fee = package.Fee,
                storageLocationId = package.StorageLocationId,
                storageLocation = package.StorageLocationCode,
                notes = package.Notes,
                receivedAtUtc = IsoUtc(package.CreatedAtUtc),
                createdAtUtc = IsoUtc(package.CreatedAtUtc),
                collectedAtUtc = IsoUtc(package.CollectedAtUtc),
                recipient = new
                {
                    recipientId = package.Recipient.RecipientId,
                    fullName = package.Recipient.FullName,
                    identifierNo = package.Recipient.IdentifierNo,
                    phoneNumber = PersonalData.MaskPhone(package.Recipient.PhoneNumber),
                    department = package.Recipient.Department
                },
                recipientName = package.Recipient.FullName
            };

            return Json(new
            {
                package = packageJson,
                statusHistory,
                notifications
            }, JsonRequestBehavior.AllowGet);
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