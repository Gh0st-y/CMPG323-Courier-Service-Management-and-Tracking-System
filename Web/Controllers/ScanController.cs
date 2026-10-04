using System.Web.Mvc;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// T10 spike: USB HID-mode scan and phone-camera QR scan, both calling the same lookup endpoint.
    /// Supports centralized scanning architecture requirements (FR-13, IR-007).
    /// </summary>
    public class ScanController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }
        [RoleAuthorize]
        [HttpPost]
        public ActionResult LookupScannedPackage(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Json(new { success = false, message = "Invalid or empty QR code identifier." });
            }

            // Spike mock verification: simulate finding packages starting with PKG or F20
            if (packageId.StartsWith("PKG-") || packageId.StartsWith("F20-"))
            {
                const string status = "Ready for Collection";

                // Explicit property assignment removed to resolve IDE0037 (Member name can be simplified)
                return Json(new
                {
                    success = true,
                    packageId,
                    status
                });
            }

            return Json(new { success = false, message = $"No package found matching '{packageId}'." });
        }

        /// <summary>
        /// API route invoked by Scripts/app/scan-demo.js (GET /api/packages/scan/{packageId}).
        /// </summary>
        [RoleAuthorize]
        [HttpGet]
        [Route("api/packages/scan/{packageId}")]
        public ActionResult ApiScanLookup(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Json(new { success = false, message = "Invalid package identifier." }, JsonRequestBehavior.AllowGet);
            }

            // Simplified object initializer syntax to satisfy Roslyn rule IDE0037
            const string status = "Ready for Collection";
            const string storageLocation = "Shelf B-04";

            return Json(new
            {
                success = true,
                packageId,
                status,
                storageLocation
            }, JsonRequestBehavior.AllowGet);
        }
    }
}