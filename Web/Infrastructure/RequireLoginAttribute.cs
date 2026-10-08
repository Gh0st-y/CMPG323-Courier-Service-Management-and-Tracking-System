using System;
using System.Web.Mvc;
using CourierService.Services.Security;

namespace CourierService.Web.Infrastructure
{
    /// <summary>
    /// Registered for every controller in Global.asax: nobody gets past the login page without logging in (FR-01).
    /// A browser opening a page is sent to /Account/Login (with ?expired=1 after a timeout) and comes back to the
    /// same page afterwards. A script asking for data gets 401 in the API's error shape instead of a login page.
    /// /api/... endpoints keep their own [RoleAuthorize] checks, and [AllowAnonymous] actions (the login page) are open.
    /// </summary>
    public class RequireLoginAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null) throw new ArgumentNullException(nameof(filterContext));

            if (filterContext.IsChildAction)
            {
                return;
            }

            var allowAnonymous =
                filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true)
                || filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true);

            var http = filterContext.HttpContext;
            var appPath = http.Request.AppRelativeCurrentExecutionFilePath ?? "~/";
            var path = appPath.StartsWith("~", StringComparison.Ordinal) ? appPath.Substring(1) : appPath;

            var decision = PageAccessRule.Evaluate(
                UserSession.IsSignedIn(http.Session), allowAnonymous, path, http.Request.Headers["Accept"]);

            if (decision == PageAccessDecision.RedirectToLogin)
            {
                // Only come back to pages that were opened normally; a form post can't be replayed by a redirect
                var returnUrl = string.Equals(http.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                    ? http.Request.RawUrl
                    : null;

                filterContext.Result = new RedirectResult(PageAccessRule.LoginUrl(returnUrl, UserSession.HasExpired(http)));
            }
            else if (decision == PageAccessDecision.NotAuthenticated)
            {
                http.Response.StatusCode = 401;
                http.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new JsonResult
                {
                    Data = new { error = new { code = "NotAuthenticated", message = "You need to log in to do this." } },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }
    }
}