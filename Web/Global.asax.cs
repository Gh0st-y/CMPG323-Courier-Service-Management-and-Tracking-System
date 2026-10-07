using System;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using CourierService.Web.Infrastructure;

namespace CourierService.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // T52: a database outage becomes 503 Service Unavailable instead of a generic error
            GlobalFilters.Filters.Add(new DatabaseUnavailableFilter());

            // Controllers read JSON request bodies themselves (RequestBody.Read) so they can answer 400 for a bad body.
            // MVC's own JSON value provider would parse the body first and throw on bad JSON, which comes out as a 500.
            var jsonProvider = ValueProviderFactories.Factories.OfType<JsonValueProviderFactory>().FirstOrDefault();
            if (jsonProvider != null)
            {
                ValueProviderFactories.Factories.Remove(jsonProvider);
            }

            // T25: sends queued notifications in the background, so staff actions never wait for the mail server
            NotificationWorker.Start();
        }


        /// T12: turns the logged-in session (set by AuthController.Login) into the request's
        /// User/IPrincipal, so standard [Authorize(Roles = "...")] attributes work against it
        /// in T13 onward. Must be PostAcquireRequestState, not PostAuthenticateRequest —
        /// Session isn't loaded yet at the authenticate stage and would read as null there.

        protected void Application_PostAcquireRequestState(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context?.Session == null)
            {
                return;
            }

            var username = context.Session["Username"] as string;
            if (string.IsNullOrEmpty(username))
            {
                return;
            }

            var roleName = context.Session["RoleName"] as string;
            var roles = string.IsNullOrEmpty(roleName) ? new string[0] : new[] { roleName };

            var identity = new GenericIdentity(username, "CourierServiceSession");
            var principal = new GenericPrincipal(identity, roles);

            context.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
        }
    }
}