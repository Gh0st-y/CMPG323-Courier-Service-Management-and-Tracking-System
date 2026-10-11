using CourierService.Web.Infrastructure;
using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var role = UserSession.RoleName(Session);

            // Same rules as the nav in _Layout.cshtml
            ViewBag.CanRegister = role == "IntakeClerk" || role == "Supervisor" || role == "SystemAdmin";
            ViewBag.CanSearch = UserSession.IsSignedIn(Session);
            ViewBag.CanCollect = role == "CollectionStaff" || role == "Supervisor" || role == "SystemAdmin";
            return View();
        }
    }
}