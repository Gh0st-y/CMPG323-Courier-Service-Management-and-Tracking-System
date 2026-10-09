using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    /// <summary>The collection screen (FR-07). The page itself only; the data and the hand-over go through PackageActionsController.</summary>
    public class CollectionController : Controller
    {
        // GET: /Collection
        [RoleAuthorize(RoleNames.CollectionStaff, RoleNames.Supervisor, RoleNames.SystemAdmin)]

        public ActionResult Index()
        {
            return View();
        }
    }
}
