using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json.Linq;

namespace CourierService.Web.Controllers
{
    [RoutePrefix("api/packages")]
    public class PackagesApiController : ApiController
    {
        private string GetFixturesPath()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(HttpRuntime.AppDomainAppPath, ".."));
            return Path.Combine(repoRoot, "frontend-mocks", "mock-fixtures.json");
        }

        // GET: api/packages/F20-0001
        [HttpGet]
        [Route("{packageId}")]
        public IHttpActionResult GetPackage(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return BadRequest("Package ID is required.");

            var fixturesPath = GetFixturesPath();
            if (!File.Exists(fixturesPath))
                return NotFound();

            var jsonText = File.ReadAllText(fixturesPath);
            var root = JObject.Parse(jsonText);

            if (!(root["packages"] is JArray packages))
                return NotFound();

            // Search across all 18 dynamic records
            var pkg = packages.FirstOrDefault(p =>
                string.Equals((string)p["packageId"] ?? (string)p["id"], packageId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals((string)p["barcode"], packageId, StringComparison.OrdinalIgnoreCase));

            if (pkg == null)
                return NotFound();

            return Ok(pkg);
        }

        // POST: api/packages/F20-0001/status
        [HttpPost]
        [Route("{packageId}/status")]
        public IHttpActionResult UpdateStatus(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return BadRequest("Package ID is required.");

            // Success response for status update
            return Ok(new { success = true, message = $"Package {packageId} updated." });
        }
    }
}