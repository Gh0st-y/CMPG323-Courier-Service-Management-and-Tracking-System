using System;
using System.Collections.Generic;

namespace CourierService.Services.Security
{
    public enum PageAccessDecision
    {
        /// <summary>Let the request through.</summary>
        Allow,

        /// <summary>A browser opening a page without being logged in: send it to the login page.</summary>
        RedirectToLogin,

        /// <summary>A script asking for data without being logged in: answer 401 in the API's error shape.</summary>
        NotAuthenticated
    }

    /// <summary>
    /// Decides what happens to a request from someone who isn't logged in (FR-01: staff "access the system only after
    /// successful authentication"). The MVC filter in the Web project just calls this, so it can be unit tested
    /// without System.Web, like RoleAccessRule and HttpsRedirectRule.
    /// </summary>
    public static class PageAccessRule
    {
        public const string LoginPath = "/Account/Login";

        /// <param name="isLoggedIn">The session has a logged-in user.</param>
        /// <param name="allowAnonymous">The action or controller is marked [AllowAnonymous], e.g. the login page.</param>
        /// <param name="path">The path inside the app, e.g. "/Packages/Search" or "/api/packages".</param>
        /// <param name="acceptHeader">The request's Accept header. Browsers opening a page ask for text/html.</param>
        public static PageAccessDecision Evaluate(bool isLoggedIn, bool allowAnonymous, string path, string acceptHeader)
        {
            if (isLoggedIn || allowAnonymous)
            {
                return PageAccessDecision.Allow;
            }

            // /api/... endpoints answer for themselves: [RoleAuthorize] gives 401 JSON, and the login endpoint must stay open
            if (IsApiPath(path))
            {
                return PageAccessDecision.Allow;
            }

            return WantsHtml(acceptHeader) ? PageAccessDecision.RedirectToLogin : PageAccessDecision.NotAuthenticated;
        }

        public static bool IsApiPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return string.Equals(path, "/api", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool WantsHtml(string acceptHeader)
        {
            return acceptHeader != null && acceptHeader.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The login page address, e.g. /Account/Login?expired=1&amp;returnUrl=%2FPackages%2FSearch. The page to come back
        /// to is only kept when it is a path on this site, so the login page can't be used to send people elsewhere.
        /// </summary>
        public static string LoginUrl(string returnUrl, bool sessionExpired)
        {
            var query = new List<string>();

            if (sessionExpired)
            {
                query.Add("expired=1");
            }

            if (IsLocalPath(returnUrl) && returnUrl != "/")
            {
                query.Add("returnUrl=" + Uri.EscapeDataString(returnUrl));
            }

            return query.Count == 0 ? LoginPath : LoginPath + "?" + string.Join("&", query);
        }

        /// <summary>"/Packages/Search?x=1" yes; "//evil.com", "/\evil.com", "https://evil.com" and "" no.</summary>
        public static bool IsLocalPath(string url)
        {
            if (string.IsNullOrEmpty(url) || url[0] != '/')
            {
                return false;
            }

            if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
            {
                return false;
            }

            return url.IndexOf('\r') < 0 && url.IndexOf('\n') < 0;
        }
    }
}