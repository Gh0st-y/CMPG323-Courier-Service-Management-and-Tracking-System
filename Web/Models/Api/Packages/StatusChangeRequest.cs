namespace CourierService.Web.Models.Api.Packages
{
    /// <summary>Body of POST /api/packages/{f20Identifier}/status (docs/API_CONTRACT.md).</summary>
    public class StatusChangeRequest
    {
        public string NewStatus { get; set; }
        public int? StorageLocationId { get; set; }
    }
}
