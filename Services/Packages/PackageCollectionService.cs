using System;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Packages
{
    public class PackageCollectionService : IPackageCollectionService
    {
        private readonly IPackageStatusService _statusService;
        private readonly IPackageRepository _packages;
        private readonly IUserRepository _users;

        public PackageCollectionService(IPackageStatusService statusService, IPackageRepository packages, IUserRepository users)
        {
            if (statusService == null) throw new ArgumentNullException(nameof(statusService));
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (users == null) throw new ArgumentNullException(nameof(users));

            _statusService = statusService;
            _packages = packages;
            _users = users;
        }

        public CollectionResult Collect(string f20Identifier, int processedByUserId, int? verifiedByUserId = null)
        {
            if (string.IsNullOrWhiteSpace(f20Identifier))
            {
                throw new ArgumentException("A package identifier is required.", nameof(f20Identifier));
            }

            var verifier = verifiedByUserId ?? processedByUserId;

            // Someone other than the processor must be a real, active user. Without this a bad id would surface as a
            // foreign key error from the database instead of a clear message.
            if (verifier != processedByUserId)
            {
                var user = _users.GetById(verifier);
                if (user == null || !user.IsActive)
                {
                    return new CollectionResult(
                        StatusChangeResult.InvalidInput(
                            f20Identifier.Trim(), PackageStatus.Collected, "verifiedByUserId must be an active staff member."),
                        null);
                }
            }

            var change = _statusService.ChangeStatus(
                f20Identifier, PackageStatus.Collected, null, processedByUserId, null, verifier);

            if (!change.Success)
            {
                return new CollectionResult(change, null);
            }

            // Read back the time the database stamped, so what we report is what was stored
            var package = _packages.GetByF20Identifier(change.F20Identifier);
            return new CollectionResult(change, package == null ? null : package.CollectedAtUtc);
        }
    }
}
