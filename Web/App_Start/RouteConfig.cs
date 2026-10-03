using System.Web.Mvc;
using System.Web.Routing;

namespace CourierService.Web
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapMvcAttributeRoutes();

            // Route for Status Updates
            routes.MapRoute(
                name: "UpdatePackageStatus",
                url: "api/packages/{id}/status",
                defaults: new { controller = "Mock", action = "UpdateStatus" }
            );

            // Route for Package Lookup
            routes.MapRoute(
                name: "ApiPackages",
                url: "api/packages/{id}",
                defaults: new { controller = "Mock", action = "GetPackage", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}