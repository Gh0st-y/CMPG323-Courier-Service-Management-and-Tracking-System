using CourierService.Domain.Entities;

namespace CourierService.Services.Packages
{
    public interface IPackageLookupService
    {
        /// <summary>
        /// Finds a package from whatever a scanner or QR code produced (FR-13, IR-006). Returns null both when no
        /// package has that identifier and when the text isn't a valid identifier at all, so callers give one
        /// friendly not-found for both and never an error.
        /// </summary>
        Package Find(string scannedValue);
    }
}
