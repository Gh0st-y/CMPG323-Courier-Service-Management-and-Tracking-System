using System.Web.Mvc;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;

namespace CourierService.Web.Controllers
{
    // T44: the user management page. All data comes from UsersApiController (T43) via users.js.
    [RoleAuthorize(RoleNames.SystemAdmin)]
    public class UserManagementController : Controller
    {
        public ActionResult Index()
        {
            // So the page can disable "deactivate" and "change role" on the admin's own row
            ViewBag.CurrentUserId = Session["UserId"] is int ? (int)Session["UserId"] : 0;
            return View();
        }
    }
}