using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;

namespace CourierService.Web.Controllers
{
    public class MockController : Controller
    {
        private string GetFixturesPath()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(HttpRuntime.AppDomainAppPath, ".."));
            return Path.Combine(repoRoot, "frontend-mocks", "mock-fixtures.json");
        }

        // GET: /Mock/Fixtures
        public ActionResult Fixtures()
        {
            var fixturesPath = GetFixturesPath();

            if (!System.IO.File.Exists(fixturesPath))
            {
                return HttpNotFound();
            }

            return Content(System.IO.File.ReadAllText(fixturesPath), "application/json");
        }

        // GET: /api/packages/{id}
        [HttpGet]
        public ActionResult GetPackage(string id)
        {
            var fixturesPath = GetFixturesPath();
            if (!System.IO.File.Exists(fixturesPath))
            {
                return HttpNotFound();
            }

            var jsonText = System.IO.File.ReadAllText(fixturesPath);
            var root = JObject.Parse(jsonText);

            // Target the "items" array nested under "GET /api/packages"
            var packages = root["GET /api/packages"]?["items"] as JArray;

            if (packages == null)
            {
                return HttpNotFound();
            }

            // Find package by matching f20Identifier (e.g. "F20-0007") or numeric packageId
            var pkg = packages.FirstOrDefault(p =>
                string.Equals((string)p["f20Identifier"], id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals((string)p["packageId"], id, StringComparison.OrdinalIgnoreCase));

            if (pkg == null)
            {
                return HttpNotFound();
            }

            return Content(pkg.ToString(), "application/json");
        }
        // POST: /api/packages/{id}/status
        [HttpPost]
        public ActionResult UpdateStatus(string id)
        {
            var fixturesPath = GetFixturesPath();
            if (!System.IO.File.Exists(fixturesPath))
            {
                return HttpNotFound();
            }

            var jsonText = System.IO.File.ReadAllText(fixturesPath);
            var root = JObject.Parse(jsonText);

            if (!(root["GET /api/packages"]?["items"] is JArray packages))
            {
                return HttpNotFound();
            }

            // Match package by f20Identifier (e.g., F20-0007) or packageId
            var pkg = packages.FirstOrDefault(p =>
                string.Equals((string)p["f20Identifier"], id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals((string)p["packageId"], id, StringComparison.OrdinalIgnoreCase));

            if (pkg == null)
            {
                return HttpNotFound();
            }

            // 1. Update the main list status
            pkg["status"] = "Collected";

            // 2. Also update detail view fixture if present
            var f20Id = (string)pkg["f20Identifier"];
            var detailKey = $"GET /api/packages/{f20Id}/detail";
            if (root[detailKey]?["package"] != null)
            {
                root[detailKey]["package"]["status"] = "Collected";
            }

            // 3. Save the modified JSON back to mock-fixtures.json on disk
            System.IO.File.WriteAllText(fixturesPath, root.ToString(Newtonsoft.Json.Formatting.Indented));

            return Content($"{{\"success\": true, \"message\": \"Package {id} marked as Collected.\"}}", "application/json");
        }
    }
}