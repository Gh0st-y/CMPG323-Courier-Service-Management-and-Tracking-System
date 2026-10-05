using System;
using CourierService.Domain;
using CourierService.Domain.Entities;

namespace CourierService.Services.Packages
{
    /// <summary>What a listener is told about a status change.</summary>
    public class PackageStatusChange
    {
        public int PackageId { get; set; }
        public string F20Identifier { get; set; }
        public PackageStatus FromStatus { get; set; }
        public PackageStatus ToStatus { get; set; }
        public int ChangedByUserId { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    /// <summary>
    /// The hook for things that react to a status change, such as the notification service (T25/T28), which
    /// subscribes by implementing this and being passed to PackageStatusService.
    ///
    /// It runs INSIDE the change's transaction, just before commit. That is deliberate: a listener that only
    /// adds a row (like queueing a notification) should use <paramref name="unitOfWork"/> so the row is saved
    /// or rolled back together with the status change. Don't do slow or external work here (sending an email,
    /// calling a gateway): queue it and let the background worker do it. If a listener throws, the whole
    /// change is rolled back.
    /// </summary>
    public interface IPackageStatusChangeListener
    {
        void OnStatusChanged(PackageStatusChange change, IUnitOfWork unitOfWork);
    }
}
