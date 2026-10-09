using CourierService.Web.Infrastructure;
using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    public class HomeController : Controller
    {
        // Skeleton landing page (T02). Replaced by the real dashboard/login flow later.
        public ActionResult Index()
        {
            var role = UserSession.RoleName(Session);

            // Same rules as the nav in _Layout.cshtml
            ViewBag.CanRegister = role == "IntakeClerk" || role == "Supervisor" || role == "SystemAdmin";
            ViewBag.CanSearch = UserSession.IsSignedIn(Session);
            ViewBag.CanCollect = role == "StorageStaff" || role == "CollectionStaff" || role == "Supervisor" || role == "SystemAdmin";
            return View();
        }
    }
}
