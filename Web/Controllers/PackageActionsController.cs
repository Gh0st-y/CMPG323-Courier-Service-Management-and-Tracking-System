using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Packages;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models.Api.Packages;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// Scan lookup, status change and collection (T19, T20; FR-04, FR-07, FR-13; docs/API_CONTRACT.md).
    /// Deliberately thin: it reads the request, checks who is calling, hands over to the services, and turns the
    /// result into the contract's responses. The rules (allowed moves, concurrency, history, audit) live in Services.
    /// One endpoint serves both the USB scanner and the camera (IR-007): they differ only in how the text got in.
    /// </summary>
    public class PackageActionsController : Controller
    {
        private readonly IPackageLookupService _lookup;
        private readonly IPackageStatusService _status;
        private readonly IPackageCollectionService _collection;
        private readonly IStorageLocationRepository _storageLocations;

        public PackageActionsController()
            : this(new SqlConnectionFactory())
        {
        }

        private PackageActionsController(IDbConnectionFactory connectionFactory)
            : this(
                CourierServices.Lookup(connectionFactory),
                CourierServices.Status(connectionFactory),
                CourierServices.Collection(connectionFactory),
                new StorageLocationRepository(connectionFactory))
        {
        }

        public PackageActionsController(
            IPackageLookupService lookup,
            IPackageStatusService status,
            IPackageCollectionService collection,
            IStorageLocationRepository storageLocations)
        {
            _lookup = lookup;
            _status = status;
            _collection = collection;
            _storageLocations = storageLocations;
        }

        /// <summary>GET /api/packages/scan/{f20Identifier}: the package record, or a friendly 404 for an unknown or malformed code (IR-006).</summary>
        [RoleAuthorize]
        [HttpGet]
        [Route("api/packages/scan/{packageId}")]
        public ActionResult Scan(string packageId)
        {
            var package = _lookup.Find(packageId);
            if (package == null)
            {
                return NotFound();
            }

            return Json(ToJson(package), JsonRequestBehavior.AllowGet);
        }

        /// <summary>POST /api/packages/{f20Identifier}/status: moves a package along its lifecycle (DR-009). Collection has its own endpoint.</summary>
        [RoleAuthorize(RoleNames.StorageStaff, RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpPost]
        [Route("api/packages/{packageId}/status")]
        public ActionResult ChangeStatus(string packageId)
        {
            int userId;
            if (!TryGetUserId(out userId))
            {
                return NotLoggedIn();
            }

            bool malformed;
            var request = RequestBody.Read<StatusChangeRequest>(Request, out malformed);
            if (malformed)
            {
                return Error(400, "ValidationError", "The request body is not valid JSON.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.NewStatus))
            {
                return Error(400, "ValidationError", "newStatus is required.");
            }

            PackageStatus newStatus;
            if (!PackageStatusParser.TryParse(request.NewStatus, out newStatus))
            {
                return Error(400, "ValidationError",
                    "newStatus must be one of: " + string.Join(", ", Enum.GetNames(typeof(PackageStatus))) + ".");
            }

            // Collection is its own step with its own roles (CollectionStaff). If it were allowed here, storage staff
            // could collect packages and the role split would mean nothing (SR-02).
            if (newStatus == PackageStatus.Collected)
            {
                return Error(400, "ValidationError", "Use POST /api/packages/{id}/collect to mark a package as collected.");
            }

            string identifier;
            if (!PackageIdentifier.TryNormalize(packageId, out identifier))
            {
                return NotFound();
            }

            var result = _status.ChangeStatus(identifier, newStatus, request.StorageLocationId, userId);
            if (!result.Success)
            {
                return FromFailure(result);
            }

            return Json(ToJson(_lookup.Find(identifier)));
        }

        /// <summary>POST /api/packages/{f20Identifier}/collect: hands the package over. Only from Ready for Collection (FR-07).</summary>
        [RoleAuthorize(RoleNames.CollectionStaff, RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpPost]
        [Route("api/packages/{packageId}/collect")]
        public ActionResult Collect(string packageId)
        {
            int userId;
            if (!TryGetUserId(out userId))
            {
                return NotLoggedIn();
            }

            bool malformed;
            var request = RequestBody.Read<CollectRequest>(Request, out malformed);
            if (malformed)
            {
                return Error(400, "ValidationError", "The request body is not valid JSON.");
            }

            string identifier;
            if (!PackageIdentifier.TryNormalize(packageId, out identifier))
            {
                return NotFound();
            }

            var result = _collection.Collect(identifier, userId, request == null ? null : request.VerifiedByUserId);
            if (!result.Success)
            {
                return FromFailure(result.Change);
            }

            return Json(new { status = PackageStatus.Collected.ToString(), collectedAtUtc = IsoUtc(result.CollectedAtUtc) });
        }

        /// <summary>GET /api/storage-locations: active locations, for the status screen to choose from.</summary>
        [RoleAuthorize]
        [HttpGet]
        [Route("api/storage-locations")]
        public ActionResult StorageLocations()
        {
            var locations = _storageLocations.GetAll(true)
                .Select(l => new { storageLocationId = l.StorageLocationId, code = l.Code, description = l.Description })
                .ToList();

            return Json(locations, JsonRequestBehavior.AllowGet);
        }

        // ---------------------------------------------------------------------------------

        protected override void OnException(ExceptionContext filterContext)
        {
            // Already answered by DatabaseUnavailableFilter (a 503 for a database outage), which runs first
            if (filterContext.ExceptionHandled)
            {
                return;
            }

            // Anything unexpected becomes the standard error shape, with no stack trace, SQL or paths in it (SR-03, OR-04)
            Trace.TraceError(filterContext.Exception.ToString());

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.StatusCode = 500;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new JsonResult
            {
                Data = new { error = new { code = "ServerError", message = "Something went wrong. Please try again." } },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        private bool TryGetUserId(out int userId)
        {
            var value = Session["UserId"];
            if (value is int)
            {
                userId = (int)value;
                return true;
            }

            userId = 0;
            return false;
        }

        private ActionResult FromFailure(StatusChangeResult result)
        {
            switch (result.Outcome)
            {
                case StatusChangeOutcome.NotFound:
                    return NotFound();
                case StatusChangeOutcome.InvalidInput:
                    return Error(400, result.ErrorCode, result.Message);
                default:
                    // InvalidTransition and ConcurrentUpdate: the request was fine but the package isn't in a state that allows it
                    return Error(409, result.ErrorCode, result.Message);
            }
        }

        private ActionResult NotFound()
        {
            return Error(404, "NotFound", "No package was found for that code.");
        }

        private ActionResult NotLoggedIn()
        {
            return Error(401, "NotAuthenticated", "You need to log in to do this.");
        }

        private ActionResult Error(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// What the scan and collection screens need to identify a package and its recipient. The phone number is masked:
        /// enough for the recipient to confirm it's theirs, not the full contact details (DR-004). No email.
        /// </summary>
        private static object ToJson(Package p)
        {
            return new
            {
                packageId = p.PackageId,
                f20Identifier = p.F20Identifier,
                status = p.Status.ToString(),
                classification = p.Classification,
                packageType = p.PackageType,
                paymentStatus = p.PaymentStatus,
                fee = p.Fee,
                storageLocationId = p.StorageLocationId,
                storageLocation = p.StorageLocationCode,
                recipientName = p.Recipient.FullName,
                recipientIdentifierNo = p.Recipient.IdentifierNo,
                recipientDepartment = p.Recipient.Department,
                recipientPhone = PersonalData.MaskPhone(p.Recipient.PhoneNumber),
                receivedAtUtc = IsoUtc(p.CreatedAtUtc),
                collectedAtUtc = IsoUtc(p.CollectedAtUtc)
            };
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