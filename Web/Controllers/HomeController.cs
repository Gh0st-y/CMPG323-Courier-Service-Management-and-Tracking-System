using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    public class HomeController : Controller
    {
        // Skeleton landing page (T02). Replaced by the real dashboard/login flow later.
        public ActionResult Index()
        {
            return View();
        }
    }
}
