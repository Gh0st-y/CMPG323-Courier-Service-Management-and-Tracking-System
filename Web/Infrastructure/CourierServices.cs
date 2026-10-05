using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Services.Audit;
using CourierService.Services.Packages;

namespace CourierService.Web.Infrastructure
{
    /// <summary>
    /// Wires the services together from the Data repositories, in one place, so each controller doesn't repeat it.
    /// Controllers also take the interfaces in a second constructor, so a different set can be passed in.
    /// </summary>
    public static class CourierServices
    {
        public static IPackageLookupService Lookup(IDbConnectionFactory connectionFactory)
        {
            return new PackageLookupService(new PackageRepository(connectionFactory));
        }

        public static IPackageStatusService Status(IDbConnectionFactory connectionFactory)
        {
            return new PackageStatusService(
                new PackageRepository(connectionFactory),
                new PackageStatusHistoryRepository(connectionFactory),
                new StorageLocationRepository(connectionFactory),
                new AuditLogger(new AuditLogRepository(connectionFactory)),
                new UnitOfWorkFactory(connectionFactory));
        }

        public static IPackageCollectionService Collection(IDbConnectionFactory connectionFactory)
        {
            return new PackageCollectionService(
                Status(connectionFactory),
                new PackageRepository(connectionFactory),
                new UserRepository(connectionFactory));
        }
    }
}
