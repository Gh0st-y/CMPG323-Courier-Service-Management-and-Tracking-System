using System;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Audit
{
    public class AuditLogQueryService : IAuditLogQueryService
    {
        public const int DefaultPageSize = 25;
        public const int MaxPageSize = 100;

        private readonly IAuditLogQueryRepository _queryRepository;

        public AuditLogQueryService(IAuditLogQueryRepository queryRepository)
        {
            if (queryRepository == null)
            {
                throw new ArgumentNullException(nameof(queryRepository));
            }

            _queryRepository = queryRepository;
        }

        public AuditLogPage Search(AuditLogFilter filter, int page, int pageSize)
        {
            if (page < 1)
            {
                throw new ArgumentException("Page must be 1 or higher.", nameof(page));
            }

            filter = filter ?? new AuditLogFilter();

            if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc.Value >= filter.ToUtc.Value)
            {
                throw new ArgumentException("The start date must be on or before the end date.", nameof(filter));
            }

            // Copy so the caller's filter is left alone
            var cleaned = new AuditLogFilter
            {
                Username = NullIfBlank(filter.Username),
                Action = NullIfBlank(filter.Action),
                FromUtc = filter.FromUtc,
                ToUtc = filter.ToUtc
            };

            if (pageSize < 1)
            {
                pageSize = DefaultPageSize;
            }

            if (pageSize > MaxPageSize)
            {
                pageSize = MaxPageSize;
            }

            var result = _queryRepository.Search(cleaned, page, pageSize);
            result.Page = page;
            result.PageSize = pageSize;
            return result;
        }

        private static string NullIfBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}