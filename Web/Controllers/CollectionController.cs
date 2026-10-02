using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;

namespace CourierService.Web.Controllers
{
    public class CollectionController : Controller
    {
        private string GetFixturesPath()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(HttpRuntime.AppDomainAppPath, ".."));
            return Path.Combine(repoRoot, "frontend-mocks", "mock-fixtures.json");
        }

        // GET: /Collection
        public ActionResult Index()
        {
            return View();
        }

        // GET: /Collection/GetPackageDetails?packageId=F20-0001
        [HttpGet]
        public ActionResult GetPackageDetails(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Json(new { success = false, message = "Package ID is required." }, JsonRequestBehavior.AllowGet);
            }

            var fixturesPath = GetFixturesPath();
            if (!System.IO.File.Exists(fixturesPath))
            {
                return Json(new { success = false, message = "Mock fixtures file not found." }, JsonRequestBehavior.AllowGet);
            }

            var jsonText = System.IO.File.ReadAllText(fixturesPath);
            var root = JObject.Parse(jsonText);
            if (!(root["packages"] is JArray packages))
            {
                return HttpNotFound();
            }

            // Find matching package from the 18 fixtures
            var pkg = packages.FirstOrDefault(p =>
                string.Equals((string)p["packageId"] ?? (string)p["id"], packageId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals((string)p["barcode"], packageId, StringComparison.OrdinalIgnoreCase));

            if (pkg == null)
            {
                return Json(new { success = false, message = "Package not found or error retrieving details." }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                packageId = (string)pkg["packageId"] ?? (string)pkg["id"],
                recipientName = (string)pkg["recipientName"],
                recipientPhone = (string)pkg["recipientPhone"],
                storageLocation = (string)pkg["storageLocation"] ?? "N/A",
                status = (string)pkg["status"] // e.g., "ReadyForCollection"
            }, JsonRequestBehavior.AllowGet);
        }
    }
}