using System;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Audit
{
    public class AuditLogger : IAuditLogger
    {
        // These match the column sizes in schema.sql
        public const int MaxActionLength = 100;
        public const int MaxEntityTypeLength = 50;
        public const int MaxEntityIdLength = 50;
        public const int MaxDetailLength = 1000;

        private readonly IAuditLogRepository _auditLogRepository;

        public AuditLogger(IAuditLogRepository auditLogRepository)
        {
            if (auditLogRepository == null)
            {
                throw new ArgumentNullException(nameof(auditLogRepository));
            }

            _auditLogRepository = auditLogRepository;
        }

        public void Log(
            string action,
            string entityType,
            string entityId,
            string detail,
            int? userId = null,
            IUnitOfWork unitOfWork = null)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("An audit entry needs an action name.", nameof(action));
            }

            if (action.Length > MaxActionLength)
            {
                throw new ArgumentException(
                    "Action name is longer than " + MaxActionLength + " characters.", nameof(action));
            }

            // Cut long text down instead of throwing. A long detail shouldn't make the real action (like registering a package) fail.
            var entry = new AuditLogEntry
            {
                UserId = userId,
                Action = action,
                EntityType = Truncate(entityType, MaxEntityTypeLength),
                EntityId = Truncate(entityId, MaxEntityIdLength),
                Detail = Truncate(detail, MaxDetailLength),
                OccurredAtUtc = DateTime.UtcNow
            };

            _auditLogRepository.Insert(entry, unitOfWork);
        }

        private static string Truncate(string value, int maxLength)
        {
            return value == null || value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }
    }
}