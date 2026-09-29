using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    public class PackagesController : Controller
    {
        /// <summary>
        /// Renders the package registration form (FR-03, UR-01).
        /// All API calls are made client-side via CourierApp.api, so this action just returns the view.
        /// </summary>
        public ActionResult Register()
        {
            return View();
        }

        /// <summary>
        /// Renders the print-friendly label view for a package (FR-03, CON-008).
        /// The f20Identifier is passed through to the view, which fetches details via CourierApp.api.
        /// </summary>
        public ActionResult Label(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Register");
            }

            return View(model: id);
        }

        /// <summary>
        /// REnders the search and results page (FR-05, UR-01).
        /// Filtering and paging happen client-side via CourierApp.api against Get/api/packages.
        /// </summary>
        public ActionResult Search()
        {
            return View();
        }

        /// <summary>
        /// Renders the package detail page with status, location, fee, timeline, and notifications (FR-05, DR-012).
        /// The f20Identifier is passed to the view, which fetches details via CourierApp.api.
        /// </summary>
        public ActionResult Detail(string id)
        {
            if(string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Search");
            }
            return View(model: id);
        }
    }
}