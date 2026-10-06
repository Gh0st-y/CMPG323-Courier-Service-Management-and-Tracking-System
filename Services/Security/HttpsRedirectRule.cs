using System;

namespace CourierService.Services.Security
{
    // Works out where a plain http:// request should be sent to reach the https:// site (T11).
    // It lives here instead of in Web so it can be unit tested without System.Web.
    public static class HttpsRedirectRule
    {
        // Returns false when the request should be left alone (odd host, bad port or odd path)
        public static bool TryBuildHttpsUrl(string host, int httpsPort, string rawUrl, out string httpsUrl)
        {
            httpsUrl = null;

            if (string.IsNullOrWhiteSpace(host) || Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                return false;
            }

            if (httpsPort < 1 || httpsPort > 65535)
            {
                return false;
            }

            if (string.IsNullOrEmpty(rawUrl) || rawUrl[0] != '/')
            {
                return false;
            }

            // Never put line breaks into a redirect header
            if (rawUrl.IndexOf('\r') >= 0 || rawUrl.IndexOf('\n') >= 0)
            {
                return false;
            }

            httpsUrl = "https://" + host + ":" + httpsPort + rawUrl;
            return true;
        }
    }
}