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
    public class PackagesController : Controller
    {
        private readonly IPackageRepository _packages;
        private readonly IPackageDetailRepository _packageDetail;

        public PackagesController()
            : this(new PackageRepository(new SqlConnectionFactory()),
                   new PackageDetailRepository(new SqlConnectionFactory()))
        {
        }

        public PackagesController(IPackageRepository packages, IPackageDetailRepository packageDetail)
        {
            _packages = packages;
            _packageDetail = packageDetail;
        }

        /// <summary>
        /// Renders the package registration form (FR-03, UR-01).
        /// All API calls are made client-side via CourierApp.api, so this action just returns the view.
        /// </summary>
        public ActionResult Register()
        {
            return View();
        }

        /// <summary>
        /// Renders the print-friendly label view for a package (FR-03, CON-008).
        /// The f20Identifier is passed through to the view, which fetches details via CourierApp.api.
        /// </summary>
        public ActionResult Label(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Register");
            }

            return View(model: id);
        }

        /// <summary>
        /// Renders the search and results page (FR-05, UR-01).
        /// Filtering and paging happen client-side via CourierApp.api against Get/api/packages.
        /// </summary>
        public ActionResult Search()
        {
            return View();
        }

        /// <summary>
        /// Renders the package detail page with status, location, fee, timeline, and notifications (FR-05, DR-012).
        /// The f20Identifier is passed to the view, which fetches details via CourierApp.api.
        /// </summary>
        public ActionResult Detail(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Search");
            }
            return View(model: id);
        }

        /// <summary>T22: GET /api/packages/{f20Identifier}/detail — JSON data for the Detail page above.</summary>
        // TODO: add [Authorize] once login (FR-01) lands.
        [HttpGet]
        [Route("api/packages/{f20Identifier}/detail")]
        public ActionResult DetailJson(string f20Identifier)
        {
            if (string.IsNullOrWhiteSpace(f20Identifier))
                return ErrorJson(400, "ValidationError", "f20Identifier is required.");

            try
            {
                var package = _packages.GetByF20Identifier(f20Identifier.Trim());
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
                        recipientAddress = n.RecipientAddress,
                        subject = n.Subject,
                        status = n.Status,
                        sentAtUtc = IsoUtc(n.SentAtUtc)
                    }).ToList();

                var packageJson = new
                {
                    packageId = package.PackageId,
                    f20Identifier = package.F20Identifier,
                    status = package.Status.ToString(),
                    classification = package.Classification,
                    packageType = package.PackageType,
                    paymentStatus = package.PaymentStatus,
                    fee = package.Fee,
                    storageLocationId = package.StorageLocationId,
                    notes = package.Notes,
                    receivedAtUtc = IsoUtc(package.CreatedAtUtc),
                    collectedAtUtc = IsoUtc(package.CollectedAtUtc),
                    recipient = new
                    {
                        recipientId = package.Recipient.RecipientId,
                        fullName = package.Recipient.FullName,
                        identifierNo = package.Recipient.IdentifierNo,
                        email = package.Recipient.Email,
                        phoneNumber = package.Recipient.PhoneNumber,
                        department = package.Recipient.Department
                    }
                };

                return Json(new
                {
                    package = packageJson,
                    statusHistory,
                    notifications
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return ErrorJson(500, "ServerError", "Something went wrong while loading the package. Please try again.");
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