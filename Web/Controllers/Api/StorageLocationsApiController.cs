using CourierService.Services.Diagnostics;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Security;
using CourierService.Services.StorageLocations;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models.Api.StorageLocations;

namespace CourierService.Web.Controllers.Api
{
    /// <summary>
    /// Storage locations (T24): the list (moved here from PackageActionsController) and adding one.
    /// Both actions must stay in this one controller: MVC refuses a URL whose attribute routes are on two
    /// different controllers, even when one is GET and the other POST.
    /// </summary>
    public class StorageLocationsApiController : Controller
    {
        private readonly IStorageLocationRepository _repository;
        private readonly StorageLocationAdminService _locations;

        public StorageLocationsApiController()
            : this(new SqlConnectionFactory())
        {
        }

        private StorageLocationsApiController(IDbConnectionFactory connectionFactory)
            : this(new StorageLocationRepository(connectionFactory), connectionFactory)
        {
        }

        private StorageLocationsApiController(IStorageLocationRepository repository, IDbConnectionFactory connectionFactory)
            : this(
                repository,
                new StorageLocationAdminService(
                    repository,
                    new AuditLogger(new AuditLogRepository(connectionFactory)),
                    new UnitOfWorkFactory(connectionFactory)))
        {
        }

        public StorageLocationsApiController(IStorageLocationRepository repository, StorageLocationAdminService locations)
        {
            _repository = repository;
            _locations = locations;
        }

        /// <summary>GET /api/storage-locations: active locations, for the Register and status screens to choose from. Any logged-in user.</summary>
        [RoleAuthorize]
        [HttpGet]
        [Route("api/storage-locations")]
        public ActionResult List()
        {
            var locations = _repository.GetAll(true)
                .Select(l => new { storageLocationId = l.StorageLocationId, code = l.Code, description = l.Description })
                .ToList();

            return Json(locations, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// POST /api/storage-locations with { code, description }: 201 { storageLocationId, code, description }.
        /// 400 ValidationError, 409 DuplicateCode. System Admin only (SR-02).
        /// </summary>
        [RoleAuthorize(RoleNames.SystemAdmin)]
        [HttpPost]
        [Route("api/storage-locations")]
        public ActionResult Create()
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
            {
                return Error(401, "NotAuthenticated", "You need to log in to do this.");
            }

            bool malformed;
            var request = RequestBody.Read<CreateStorageLocationRequest>(Request, out malformed);
            if (malformed)
            {
                return Error(400, "ValidationError", "The request body is not valid JSON.");
            }

            CreateLocationResult result;
            try
            {
                result = _locations.Create(request == null ? null : request.Code, request == null ? null : request.Description, userId.Value);
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                // Someone else added the same code between the check and the insert; the transaction was rolled back
                return Error(409, "DuplicateCode", "A storage location with that code already exists.");
            }

            if (result.Outcome == CreateLocationOutcome.Invalid)
            {
                return Error(400, "ValidationError", result.Message);
            }

            if (result.Outcome == CreateLocationOutcome.DuplicateCode)
            {
                return Error(409, "DuplicateCode", result.Message);
            }

            Response.StatusCode = 201;
            return Json(new
            {
                storageLocationId = result.Location.StorageLocationId,
                code = result.Location.Code,
                description = result.Location.Description
            });
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            // Already answered by DatabaseUnavailableFilter (a 503 for a database outage), which runs first
            if (filterContext.ExceptionHandled)
            {
                return;
            }

            // Anything unexpected becomes the standard error shape, with no stack trace, SQL or paths in it (SR-03)
            Trace.TraceError(ExceptionDiagnostics.Format(filterContext.Exception));

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.StatusCode = 500;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new JsonResult
            {
                Data = new { error = new { code = "ServerError", message = "Something went wrong. Please try again." } },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        private ActionResult Error(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } });
        }
    }
}
