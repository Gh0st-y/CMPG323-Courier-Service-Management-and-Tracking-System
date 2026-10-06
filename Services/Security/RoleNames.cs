namespace CourierService.Services.Security
{
    // Role names exactly as stored in dbo.Roles and listed in docs/API_CONTRACT.md.
    // They are constants so they can be used inside attributes like [RoleAuthorize(...)].
    public static class RoleNames
    {
        public const string IntakeClerk = "IntakeClerk";
        public const string StorageStaff = "StorageStaff";
        public const string CollectionStaff = "CollectionStaff";
        public const string Supervisor = "Supervisor";
        public const string SystemAdmin = "SystemAdmin";
    }
}