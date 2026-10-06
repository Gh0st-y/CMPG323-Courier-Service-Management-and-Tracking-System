using System.Collections.Generic;
using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    public interface IPackageStatusHistoryRepository
    {
        /// <summary>
        /// Appends one history row. Pass the same <paramref name="unitOfWork"/> as the status update so
        /// the change and its history commit (or roll back) together. There is no update or delete on
        /// purpose: history is a record of what happened.
        /// </summary>
        void Insert(PackageStatusHistoryEntry entry, IUnitOfWork unitOfWork = null);

        /// <summary>Oldest first, for the package detail timeline (DR-012).</summary>
        IEnumerable<PackageStatusHistoryEntry> GetByPackageId(int packageId);
    }
}
