using System;
using System.Data;

namespace CourierService.Domain
{
    /// <summary>
    /// A single connection + transaction shared across multiple repository calls, so a
    /// multi-step operation (e.g. status change + history row + audit entry) commits
    /// atomically as one transaction, per the project's conventions.
    ///
    /// Usage:
    /// <code>
    /// using (var uow = unitOfWorkFactory.Begin())
    /// {
    ///     packageRepository.UpdateStatus(packageId, newStatus, uow);
    ///     auditLogRepository.Insert(entry, uow);
    ///     uow.Commit();
    /// } // rolls back automatically if Commit() was never reached
    /// </code>
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IDbConnection Connection { get; }
        IDbTransaction Transaction { get; }

        /// <summary>Commits the transaction. Must be the last call made on this instance.</summary>
        void Commit();
    }
}
