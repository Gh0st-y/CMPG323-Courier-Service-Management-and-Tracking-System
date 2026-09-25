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
    /// <summary>T22: GET /api/packages/{f20Identifier}/detail.</summary>
    // TODO: add [Authorize] once login (FR-01) lands.
    // NOTE: T21 (search endpoint) also creates this file, with GET /api/packages (Search).
    // These two branches will conflict on this file when merged — combine both actions then.
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

        [HttpGet]
        [Route("api/packages/{f20Identifier}/detail")]
        public ActionResult Detail(string f20Identifier)
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