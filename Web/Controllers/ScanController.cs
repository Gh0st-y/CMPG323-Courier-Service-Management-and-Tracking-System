using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// The scan screen (FR-13, IR-007). The page itself only; lookups and status changes go through
    /// PackageActionsController (GET /api/packages/scan/{id}, POST /api/packages/{id}/status).
    /// </summary>
    public class ScanController : Controller
    {
        [HttpGet]
        [RoleAuthorize(RoleNames.StorageStaff, RoleNames.Supervisor, RoleNames.SystemAdmin)]

        public ActionResult Index()
        {
            return View();
        }
    }
}
