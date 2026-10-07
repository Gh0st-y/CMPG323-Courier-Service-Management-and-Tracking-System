using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Entities;
using CourierService.Services.Audit;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;

namespace CourierService.Web.Controllers.Api
{
    // GET /api/audit-log (T45, SR-04). Supervisor and SystemAdmin only.
    public class AuditLogController : Controller
    {
        private readonly IAuditLogQueryService _queryService;

        public AuditLogController()
        {
            var connectionFactory = new SqlConnectionFactory();
            _queryService = new AuditLogQueryService(new AuditLogQueryRepository(connectionFactory));
        }
        [RoleAuthorize(RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpGet]
        [Route("api/audit-log")]
        public ActionResult Search()
        {
            // Read from the query string directly. A method parameter called "action" would be
            // filled from the route value (the controller's action name) and not from the URL.
            var query = Request.QueryString;

            var page = 1;
            if (!string.IsNullOrWhiteSpace(query["page"])
                && (!int.TryParse(query["page"], NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1))
            {
                return ApiError(400, "ValidationError", "Page must be a whole number of 1 or more.");
            }

            var pageSize = AuditLogQueryService.DefaultPageSize;
            if (!string.IsNullOrWhiteSpace(query["pageSize"])
                && (!int.TryParse(query["pageSize"], NumberStyles.None, CultureInfo.InvariantCulture, out pageSize) || pageSize < 1))
            {
                return ApiError(400, "ValidationError", "Page size must be a whole number of 1 or more.");
            }

            DateTime? dateFrom;
            DateTime? dateTo;
            if (!TryParseDate(query["dateFrom"], out dateFrom) || !TryParseDate(query["dateTo"], out dateTo))
            {
                return ApiError(400, "ValidationError", "Dates must look like 2026-09-30.");
            }

            var filter = new AuditLogFilter
            {
                Username = query["user"],
                Action = query["action"],
                FromUtc = dateFrom,
                // dateTo is a whole day, so the range stops at the start of the next day
                ToUtc = dateTo.HasValue ? dateTo.Value.AddDays(1) : (DateTime?)null
            };

            AuditLogPage result;
            try
            {
                result = _queryService.Search(filter, page, pageSize);
            }
            catch (ArgumentException ex)
            {
                return ApiError(400, "ValidationError", ex.Message);
            }

            return Json(new
            {
                items = result.Items.Select(ToApiItem).ToList(),
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            }, JsonRequestBehavior.AllowGet);
        }

        // Empty is fine (no filter). Something that isn't a yyyy-MM-dd date is not.
        private static bool TryParseDate(string raw, out DateTime? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            DateTime parsed;
            if (!DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return false;
            }

            value = parsed;
            return true;
        }

        // Field names auditId, userId, action, context and timestampUtc match what the
        // frontend mock in mock-fixtures.json already uses. The rest are extras.
        private static object ToApiItem(AuditLogEntry entry)
        {
            return new
            {
                auditId = entry.AuditLogId,
                userId = entry.UserId,
                username = entry.Username,
                action = entry.Action,
                entityType = entry.EntityType,
                entityId = entry.EntityId,
                detail = entry.Detail,
                context = BuildContext(entry),
                timestampUtc = DateTime.SpecifyKind(entry.OccurredAtUtc, DateTimeKind.Utc)
                    .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            };
        }

        private static string BuildContext(AuditLogEntry entry)
        {
            var text = ((entry.EntityType ?? "") + " " + (entry.EntityId ?? "")).Trim();
            return text.Length > 0 ? text : entry.Detail;
        }

        // Same error shape as docs/API_CONTRACT.md. GET responses need AllowGet.
        private ActionResult ApiError(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            return Json(new { error = new { code, message } }, JsonRequestBehavior.AllowGet);
        }
    }
}