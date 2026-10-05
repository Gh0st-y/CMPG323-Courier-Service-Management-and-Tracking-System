using System;
using System.Collections.Generic;
using System.Linq;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;

namespace CourierService.Services.Packages
{
    public class PackageStatusService : IPackageStatusService
    {
        // Matches PackageStatusHistory.Notes in schema.sql
        public const int MaxNotesLength = 300;

        private readonly IPackageRepository _packages;
        private readonly IPackageStatusHistoryRepository _history;
        private readonly IAuditLogger _auditLogger;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;
        private readonly IReadOnlyList<IPackageStatusChangeListener> _listeners;

        public PackageStatusService(
            IPackageRepository packages,
            IPackageStatusHistoryRepository history,
            IAuditLogger auditLogger,
            IUnitOfWorkFactory unitOfWorkFactory,
            IEnumerable<IPackageStatusChangeListener> listeners = null)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (auditLogger == null) throw new ArgumentNullException(nameof(auditLogger));
            if (unitOfWorkFactory == null) throw new ArgumentNullException(nameof(unitOfWorkFactory));

            _packages = packages;
            _history = history;
            _auditLogger = auditLogger;
            _unitOfWorkFactory = unitOfWorkFactory;
            _listeners = (listeners ?? Enumerable.Empty<IPackageStatusChangeListener>()).ToList();
        }

        public StatusChangeResult ChangeStatus(
            string f20Identifier,
            PackageStatus newStatus,
            int? storageLocationId,
            int changedByUserId,
            string notes = null)
        {
            if (string.IsNullOrWhiteSpace(f20Identifier))
            {
                throw new ArgumentException("A package identifier is required.", nameof(f20Identifier));
            }

            if (!Enum.IsDefined(typeof(PackageStatus), newStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(newStatus), "Not a known package status.");
            }

            f20Identifier = f20Identifier.Trim();

            var package = _packages.GetByF20Identifier(f20Identifier);
            if (package == null)
            {
                return StatusChangeResult.NotFound(f20Identifier, newStatus);
            }

            var from = package.Status;

            // The rule check (DR-009). Nothing has been written at this point, so a rejection leaves no trace.
            if (!PackageStatusTransitions.IsAllowed(from, newStatus))
            {
                return StatusChangeResult.InvalidTransition(package.F20Identifier, from, newStatus);
            }

            var changedAtUtc = DateTime.UtcNow;
            var collectedBy = newStatus == PackageStatus.Collected ? changedByUserId : (int?)null;

            // Everything below shares one transaction. If we leave this block without reaching Commit(), including
            // by an exception, Dispose() rolls the lot back (DR-010).
            using (var unitOfWork = _unitOfWorkFactory.Begin())
            {
                // Only succeeds if the package is still exactly as we read it (NFR-023). If two people act on the
                // same package at once, one of them gets false here and nothing is written for them.
                if (!_packages.TryUpdateStatus(
                        package.PackageId, newStatus, storageLocationId, collectedBy, package.RowVersion, unitOfWork))
                {
                    return StatusChangeResult.ConcurrentUpdate(package.F20Identifier, from, newStatus);
                }

                _history.Insert(new PackageStatusHistoryEntry
                {
                    PackageId = package.PackageId,
                    FromStatus = from,
                    ToStatus = newStatus,
                    ChangedByUserId = changedByUserId,
                    ChangedAtUtc = changedAtUtc,
                    Notes = Truncate(notes, MaxNotesLength)
                }, unitOfWork);

                // Detail has statuses only. No names or contact details in the audit log (SR-03).
                _auditLogger.Log(
                    newStatus == PackageStatus.Collected ? AuditActions.PackageCollected : AuditActions.PackageStatusChanged,
                    AuditEntityTypes.Package,
                    package.F20Identifier,
                    PackageStatusTransitions.DisplayName(from) + " -> " + PackageStatusTransitions.DisplayName(newStatus),
                    changedByUserId,
                    unitOfWork);

                var change = new PackageStatusChange
                {
                    PackageId = package.PackageId,
                    F20Identifier = package.F20Identifier,
                    FromStatus = from,
                    ToStatus = newStatus,
                    ChangedByUserId = changedByUserId,
                    ChangedAtUtc = changedAtUtc
                };

                foreach (var listener in _listeners)
                {
                    listener.OnStatusChanged(change, unitOfWork);
                }

                unitOfWork.Commit();
            }

            return StatusChangeResult.Changed(package.F20Identifier, from, newStatus);
        }

        private static string Truncate(string value, int maxLength)
        {
            return value == null || value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }
    }
}
