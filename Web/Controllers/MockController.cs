using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;

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
        [RoleAuthorize]
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
        [RoleAuthorize]
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
        // The real status endpoint is T19. This used to answer "marked as Collected" for any request, whatever
        // status was asked for, and rewrote the git-tracked mock-fixtures.json on disk. It now says plainly that
        // it isn't built yet. Role checks stay, so the contract's access rules still apply (SR-02).
        [RoleAuthorize(RoleNames.StorageStaff, RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpPost]
        public ActionResult UpdateStatus(string id)
        {
            Response.StatusCode = 501;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code = "NotImplemented", message = "Status updates are not available yet." } });
        }
    }
}
