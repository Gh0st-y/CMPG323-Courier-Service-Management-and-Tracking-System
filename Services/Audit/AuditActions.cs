namespace CourierService.Services.Audit
{
    // Values for the Action column in dbo.AuditLog. Use these instead of typing the
    // string by hand so filtering on the audit screen matches. Add new ones as features land.
    public static class AuditActions
    {
        public const string LoginSuccess = "LoginSuccess";
        public const string LoginFailed = "LoginFailed";
        public const string PackageCreated = "PackageCreated";
        public const string PackageStatusChanged = "PackageStatusChanged";
        public const string PackageCollected = "PackageCollected";
        public const string CsvImported = "CsvImported";
        public const string ConfigChanged = "ConfigChanged";
        public const string UserCreated = "UserCreated";
        public const string UserRoleChanged = "UserRoleChanged";
        public const string UserDeactivated = "UserDeactivated";
        public const string UserReactivated = "UserReactivated";
        public const string PaymentStatusChanged = "PaymentStatusChanged";
    }

    // Values for the EntityType column.
    public static class AuditEntityTypes
    {
        public const string User = "User";
        public const string Package = "Package";
        public const string CsvImport = "CsvImport";
        public const string Config = "Config";
    }
}