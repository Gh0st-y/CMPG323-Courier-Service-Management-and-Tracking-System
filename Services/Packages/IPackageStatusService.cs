using CourierService.Domain.Entities;

namespace CourierService.Services.Packages
{
    public interface IPackageStatusService
    {
        /// <summary>
        /// Moves a package to <paramref name="newStatus"/> if the rules allow it (DR-009). The status change,
        /// its history row and its audit entry are saved as one transaction, or not at all (DR-010).
        /// A package that can't be found, a move that isn't allowed, and a package someone else just changed
        /// all come back as a <see cref="StatusChangeResult"/> that isn't a success, not as exceptions.
        /// </summary>
        /// <param name="storageLocationId">Optional. Leaves the current location alone when null.</param>
        /// <param name="changedByUserId">Who is doing it. Goes on the history row and the audit entry, and on the package when it's collected.</param>
        StatusChangeResult ChangeStatus(
            string f20Identifier,
            PackageStatus newStatus,
            int? storageLocationId,
            int changedByUserId,
            string notes = null);
    }
}
