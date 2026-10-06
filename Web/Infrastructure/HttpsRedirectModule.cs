using System;
using System.Configuration;
using System.Web;
using CourierService.Services.Security;

namespace CourierService.Web.Infrastructure
{
    // Sends plain http:// requests to the https:// address (T11, SR-03, NFR-017).
    // Switch it off or change the port with Https.RedirectEnabled and Https.Port in Web.config.
    public class HttpsRedirectModule : IHttpModule
    {
        public void Init(HttpApplication application)
        {
            application.BeginRequest += OnBeginRequest;
        }

        public void Dispose()
        {
        }

        private static void OnBeginRequest(object sender, EventArgs e)
        {
            var application = (HttpApplication)sender;
            var request = application.Request;

            if (request.IsSecureConnection)
            {
                return;
            }

            var enabled = ConfigurationManager.AppSettings["Https.RedirectEnabled"];
            if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int port;
            if (!int.TryParse(ConfigurationManager.AppSettings["Https.Port"], out port))
            {
                return;
            }

            string target;
            if (!HttpsRedirectRule.TryBuildHttpsUrl(request.Url.Host, port, request.RawUrl, out target))
            {
                return;
            }

            // 307 keeps the method, so a POST to the old http address is not turned into a GET
            var response = application.Response;
            response.TrySkipIisCustomErrors = true;
            response.StatusCode = 307;
            response.RedirectLocation = target;
            application.CompleteRequest();
        }
    }
}