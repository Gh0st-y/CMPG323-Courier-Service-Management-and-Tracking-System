using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data.Repositories;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Web.Models;

namespace CourierService.Web.Controllers
{
    //[Authorize(Roles = "Supervisor, SystemAdmin")] // Hidden from non-admin/supervisor roles (Acceptance Criteria & SR-04)
    public class AuditLogController : Controller
    {
        private readonly IAuditLogRepository _auditLogRepository;

        public AuditLogController()
        {
            // Default parameterless constructor initializing repository via ADO.NET connection factory
            _auditLogRepository = new AuditLogRepository(new CourierService.Data.SqlConnectionFactory());
        }

        public AuditLogController(IAuditLogRepository auditLogRepository)
        {
            _auditLogRepository = auditLogRepository;
        }

        [HttpGet]
        public ActionResult Index(string searchTerm, string actionType, DateTime? startDate, DateTime? endDate)
        {
            // Fetch top recent entries (SR-04)
            var entries = _auditLogRepository.GetRecent(500) ?? Enumerable.Empty<AuditLogEntry>();

            // Distinct action list for dropdown filter
            var availableActions = entries
                .Select(e => e.Action)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .Select(a => new SelectListItem { Text = a, Value = a, Selected = (a == actionType) })
                .ToList();

            availableActions.Insert(0, new SelectListItem { Text = "-- All Action Types --", Value = "" });

            // Apply Filters
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                entries = entries.Where(e =>
                    (e.Action != null && e.Action.ToLower().Contains(term)) ||
                    (e.EntityType != null && e.EntityType.ToLower().Contains(term)) ||
                    (e.EntityId != null && e.EntityId.ToLower().Contains(term)) ||
                    (e.Detail != null && e.Detail.ToLower().Contains(term)) ||
                    (e.UserId.HasValue && e.UserId.Value.ToString().Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(actionType))
            {
                entries = entries.Where(e => string.Equals(e.Action, actionType, StringComparison.OrdinalIgnoreCase));
            }

            if (startDate.HasValue)
            {
                entries = entries.Where(e => e.OccurredAtUtc.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                entries = entries.Where(e => e.OccurredAtUtc.Date <= endDate.Value.Date);
            }

            var model = new AuditLogViewModel
            {
                Entries = entries.ToList(),
                SearchTerm = searchTerm,
                ActionType = actionType,
                StartDate = startDate,
                EndDate = endDate,
                ActionTypes = availableActions
            };

            return View(model);
        }
    }
}