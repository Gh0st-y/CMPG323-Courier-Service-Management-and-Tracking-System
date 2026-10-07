namespace CourierService.Services.Packages
{
    /// <summary>What the intake clerk enters on the registration form (FR-03), as received from the API.</summary>
    public class PackageRegistrationRequest
    {
        public string RecipientFullName { get; set; }
        public string RecipientIdentifierNo { get; set; }
        public string RecipientEmail { get; set; }
        public string RecipientPhone { get; set; }
        public string RecipientDepartment { get; set; }
        public string SenderName { get; set; }
        public string PackageType { get; set; }
        public string Classification { get; set; }
        public int? StorageLocationId { get; set; }
        public string Notes { get; set; }
    }
}