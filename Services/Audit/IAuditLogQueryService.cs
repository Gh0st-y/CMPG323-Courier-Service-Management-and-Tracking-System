using CourierService.Domain.Entities;

namespace CourierService.Services.Audit
{
    // Used by the audit log viewer endpoint (T45)
    public interface IAuditLogQueryService
    {
        AuditLogPage Search(AuditLogFilter filter, int page, int pageSize);
    }
}