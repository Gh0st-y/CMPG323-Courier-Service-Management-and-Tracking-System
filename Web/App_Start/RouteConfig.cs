using System.Web.Mvc;
using System.Web.Routing;

namespace CourierService.Web
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            
            // Lets controllers declare URLs like [Route("api/packages")]
            routes.MapMvcAttributeRoutes();

            // T12: enables [Route("api/...")] attributes on controllers (AuthController,
            // ScanController). Must come before the conventional routes below.
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