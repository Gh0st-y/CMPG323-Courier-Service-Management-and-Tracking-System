using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    /// <summary>The collection screen (FR-07). The page itself only; the data and the hand-over go through PackageActionsController.</summary>
    public class CollectionController : Controller
    {
        // GET: /Collection
        public ActionResult Index()
        {
            return View();
        }
    }
}
