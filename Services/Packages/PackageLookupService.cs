using System;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Packages
{
    public class PackageLookupService : IPackageLookupService
    {
        private readonly IPackageRepository _packages;

        public PackageLookupService(IPackageRepository packages)
        {
            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            _packages = packages;
        }

        public Package Find(string scannedValue)
        {
            // A malformed code never reaches the database
            string identifier;
            if (!PackageIdentifier.TryNormalize(scannedValue, out identifier))
            {
                return null;
            }

            return _packages.GetByF20Identifier(identifier);
        }
    }
}
