using System.Web.Mvc;

namespace CourierService.Web.Controllers
{
    /// <summary>T10 spike: USB HID-mode scan and phone-camera QR scan, both calling the same lookup endpoint.</summary>
    public class ScanController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
