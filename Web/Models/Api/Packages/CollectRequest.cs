namespace CourierService.Web.Models.Api.Packages
{
    /// <summary>Body of POST /api/packages/{f20Identifier}/collect. The whole body is optional.</summary>
    public class CollectRequest
    {
        /// <summary>The staff member who verified the collector's ID. Defaults to the logged-in user.</summary>
        public int? VerifiedByUserId { get; set; }
    }
}
