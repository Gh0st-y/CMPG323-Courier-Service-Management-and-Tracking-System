namespace CourierService.Services.Packages
{
    /// <summary>Registers a new package at intake (FR-03, FR-15, FR-17). The role check is on the controller.</summary>
    public interface IPackageRegistrationService
    {
        PackageRegistrationResult Register(PackageRegistrationRequest request, int registeredByUserId);
    }
}