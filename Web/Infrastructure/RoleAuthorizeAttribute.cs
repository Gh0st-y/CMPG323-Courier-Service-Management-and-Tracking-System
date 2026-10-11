using System;
using System.Web.Mvc;
using CourierService.Services.Security;

namespace CourierService.Web.Infrastructure
{
    // Role check for controllers and actions (T13, SR-02).
    //   [RoleAuthorize]                                   any logged in user
    //   [RoleAuthorize(RoleNames.Supervisor, ...)]        only the listed roles
    // Not logged in gives 401, wrong role gives 403, both in the standard error shape
    // from docs/API_CONTRACT.md. The role comes from the session set by AuthController.Login.
    // If a class and one of its actions both have the attribute, both checks run.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RoleAuthorizeAttribute : FilterAttribute, IAuthorizationFilter
    {
        private readonly string[] _allowedRoles;

        public RoleAuthorizeAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles ?? new string[0];
        }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            var session = filterContext.HttpContext.Session;
            var role = session == null ? null : session["RoleName"] as string;

            var decision = RoleAccessRule.Evaluate(role, _allowedRoles);
            if (decision == AccessDecision.Allowed)
            {
                return;
            }

            if (decision == AccessDecision.NotAuthenticated)
            {
                filterContext.Result = ErrorResult(filterContext, 401, "NotAuthenticated",
                    "You need to log in to do this.");
            }
            else
            {
                // Pages get the friendly Access Denied view; API calls keep the JSON error
                filterContext.Result = IsApiRequest(filterContext)
                    ? ErrorResult(filterContext, 403, "Forbidden", "You do not have permission to do this.")
                    : AccessDeniedPage(filterContext);
            }
        }

        private static ActionResult ErrorResult(AuthorizationContext context, int statusCode, string code, string message)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = statusCode;
            response.TrySkipIisCustomErrors = true;

            return new JsonResult
            {
                Data = new { error = new { code, message } },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        private static bool IsApiRequest(AuthorizationContext context)
        {
            var path = context.HttpContext.Request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
            return path.StartsWith("~/api/", StringComparison.OrdinalIgnoreCase);
        }

        private static ActionResult AccessDeniedPage(AuthorizationContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = 403;
            response.TrySkipIisCustomErrors = true;

            return new ViewResult { ViewName = "~/Views/Shared/accessDenied.cshtml" };
        }
    }
}