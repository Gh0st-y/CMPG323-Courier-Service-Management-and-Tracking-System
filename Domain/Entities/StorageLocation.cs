namespace CourierService.Domain.Entities
{
    /// <summary>Maps to dbo.StorageLocations.</summary>
    public class StorageLocation
    {
        public int StorageLocationId { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }
}
