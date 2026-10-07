namespace CourierService.Web.Models.Api.Packages
{
    /// <summary>Body of POST /api/packages, as sent by the registration form (docs/API_CONTRACT.md).</summary>
    public class RegisterPackageRequest
    {
        public RecipientDetails Recipient { get; set; }
        public string SenderName { get; set; }
        public string PackageType { get; set; }
        public string Classification { get; set; }
        public int? StorageLocationId { get; set; }
        public string Notes { get; set; }

        public class RecipientDetails
        {
            public string FullName { get; set; }
            public string IdentifierNo { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Department { get; set; }
        }
    }
}