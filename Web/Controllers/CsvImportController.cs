using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    public class CsvImportController : Controller
    {
        // GET: /CsvImport
        [RoleAuthorize(RoleNames.Supervisor, RoleNames.SystemAdmin)]
        public ActionResult Index()
        {

            return View();
        }
    }
}