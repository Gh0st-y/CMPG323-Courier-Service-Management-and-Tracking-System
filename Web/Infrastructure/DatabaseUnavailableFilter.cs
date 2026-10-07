using System;
using System.Diagnostics;
using System.Web.Mvc;
using CourierService.Data;

namespace CourierService.Web.Infrastructure
{
    /// <summary>
    /// Turns "the database is down" into 503 Service Unavailable instead of a generic error (T52, NFR-022, OR-04).
    /// API calls get the standard error JSON, which the screens already show in a toast. Page requests get the
    /// ServiceUnavailable page. Anything else is left alone, so real bugs still surface as 500s.
    /// Registered globally in Global.asax. Controllers that override OnException must skip exceptions this
    /// filter has already handled, because they run after it.
    /// </summary>
    public class DatabaseUnavailableFilter : IExceptionFilter
    {
        public const string ErrorCode = "ServiceUnavailable";

        public const string Message =
            "The system can't reach its database right now. Please try again in a few minutes.";

        // Seconds, sent as Retry-After so clients know when to try again
        public const string RetryAfterSeconds = "60";

        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext == null || filterContext.ExceptionHandled || !DatabaseOutage.IsOutage(filterContext.Exception))
            {
                return;
            }

            // The full error goes to the log only, never to the user (SR-03)
            Trace.TraceError("Database unavailable: " + filterContext.Exception);

            var httpContext = filterContext.HttpContext;
            var response = httpContext.Response;
            response.StatusCode = 503;
            response.TrySkipIisCustomErrors = true;
            response.AppendHeader("Retry-After", RetryAfterSeconds);

            if (IsApiRequest(httpContext.Request.Path) || httpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new JsonResult
                {
                    Data = new { error = new { code = ErrorCode, message = Message } },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                filterContext.Result = new ViewResult { ViewName = "ServiceUnavailable" };
            }

            filterContext.ExceptionHandled = true;
        }

        public static bool IsApiRequest(string path)
        {
            return path != null
                && (path.Equals("/api", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase));
        }
    }
}