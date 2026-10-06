using CourierService.Domain.Entities;
using CourierService.Domain.Models;

namespace CourierService.Domain.Repositories
{
    public interface IPackageRepository
    {
        /// <summary>
        /// Looks up a package by its QR/scan identifier (CON-008), including recipient and
        /// storage location details. Returns null if not found — callers turn that into the
        /// friendly 404 required by IR-006, never a raw exception.
        /// </summary>
        Package GetByF20Identifier(string f20Identifier);

        Package GetById(int packageId);

        /// <summary>Inserts a new package and returns its generated PackageId.</summary>
        int Insert(Package package, IUnitOfWork unitOfWork = null);

        /// <summary>
        /// Updates Status (and optionally StorageLocationId). Does not write history or audit
        /// rows — callers combine this with IPackageStatusHistoryRepository/IAuditLogRepository
        /// calls sharing the same <paramref name="unitOfWork"/> so the whole change is one commit.
        /// </summary>
        void UpdateStatus(int packageId, PackageStatus newStatus, int? storageLocationId, IUnitOfWork unitOfWork = null);

        /// <summary>
        /// Status change with optimistic concurrency (NFR-023). Only updates the row if it still has
        /// <paramref name="expectedRowVersion"/>, i.e. nobody has changed it since it was read. Returns
        /// false if the row was changed in the meantime (or no longer exists); the caller must then
        /// re-read and decide again. Moving to Collected also stamps CollectedAtUtc and
        /// <paramref name="collectedByUserId"/>. Does not check that the transition is allowed — that is
        /// the service layer's job (DR-009); use PackageStatusService rather than calling this directly.
        /// </summary>
        bool TryUpdateStatus(
            int packageId,
            PackageStatus newStatus,
            int? storageLocationId,
            int? collectedByUserId,
            byte[] expectedRowVersion,
            IUnitOfWork unitOfWork = null);

        /// <summary>
        /// Filtered, paged package search (T21). Newest first. Parameterised queries only.
        /// </summary>
        PagedResult<Package> Search(PackageSearchCriteria criteria);
    }
}
