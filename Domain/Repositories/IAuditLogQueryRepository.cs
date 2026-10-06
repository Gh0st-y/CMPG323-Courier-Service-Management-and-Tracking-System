using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    // Read side of the audit log. Kept apart from IAuditLogRepository so that one stays
    // write only (SR-04, NFR-019). Nothing in here changes data.
    public interface IAuditLogQueryRepository
    {
        // Newest entries first. page starts at 1.
        AuditLogPage Search(AuditLogFilter filter, int page, int pageSize);
    }
}