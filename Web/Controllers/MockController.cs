using System.IO;
using System.Web;
using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// Dev-only convenience: serves frontend-mocks/mock-fixtures.json (single source of truth,
    /// shared with T09/other FE work) to the browser. IIS/IIS Express won't otherwise serve it,
    /// since that folder lives outside the Web project's own directory. Not part of the real
    /// API contract (docs/API_CONTRACT.md) — the shared JS fetch wrapper only calls this when
    /// mock mode is switched on (T07).
    /// </summary>
    public class MockController : Controller
    {
        public ActionResult Fixtures()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(HttpRuntime.AppDomainAppPath, ".."));
            var fixturesPath = Path.Combine(repoRoot, "frontend-mocks", "mock-fixtures.json");

            if (!System.IO.File.Exists(fixturesPath))
            {
                return HttpNotFound();
            }

            return Content(System.IO.File.ReadAllText(fixturesPath), "application/json");
        }
    }
}
