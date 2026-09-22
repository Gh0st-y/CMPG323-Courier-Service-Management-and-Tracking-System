using System.Collections.Generic;
using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    public interface IStorageLocationRepository
    {
        IEnumerable<StorageLocation> GetAll(bool activeOnly = true);

        StorageLocation GetById(int storageLocationId);

        int Insert(StorageLocation location, IUnitOfWork unitOfWork = null);
    }
}
