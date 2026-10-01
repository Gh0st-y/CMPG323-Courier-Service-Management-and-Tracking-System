using System;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace CourierService.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
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