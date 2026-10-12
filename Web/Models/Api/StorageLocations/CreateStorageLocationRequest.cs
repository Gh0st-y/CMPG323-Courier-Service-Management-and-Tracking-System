namespace CourierService.Web.Models.Api.StorageLocations
{
    /// <summary>Body of POST /api/storage-locations (docs/API_CONTRACT.md).</summary>
    public class CreateStorageLocationRequest
    {
        /// <summary>Required, up to 50 characters, unique, e.g. "Shelf D-1".</summary>
        public string Code { get; set; }

        /// <summary>Optional, up to 200 characters.</summary>
        public string Description { get; set; }
    }
}
