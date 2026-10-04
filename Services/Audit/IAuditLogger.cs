using CourierService.Domain;

namespace CourierService.Services.Audit
{
    // Services use this to write to the audit log (SR-04, NFR-019).
    // There is no update or delete on purpose. To fix a wrong entry, log a new one.
    public interface IAuditLogger
    {
        // userId can be null when nobody is logged in, e.g. a failed login for a username that doesn't exist.
        // Pass the unitOfWork when you are inside a transaction so the entry rolls back with it.
        // Keep detail free of personal info like names, emails and phone numbers (SR-03).
        void Log(
            string action,
            string entityType,
            string entityId,
            string detail,
            int? userId = null,
            IUnitOfWork unitOfWork = null);
    }
}