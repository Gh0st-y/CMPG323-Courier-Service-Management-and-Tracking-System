using System.Collections.Generic;
using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    public interface IAuditLogRepository
    {
        /// <summary>
        /// Appends one entry. There is deliberately no Update/Delete method on this interface —
        /// the audit log is append-only (SR-04, NFR-019).
        /// </summary>
        void Insert(AuditLogEntry entry, IUnitOfWork unitOfWork = null);

        /// <summary>Most recent entries first, for the audit log screen (SR-04).</summary>
        IEnumerable<AuditLogEntry> GetRecent(int take);
    }
}
