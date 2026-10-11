using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// MVC UI Controller for viewing system activity and security audit logs (SR-04, T46).
    /// </summary>
    [RoleAuthorize(RoleNames.Supervisor, RoleNames.SystemAdmin)]
    public class AuditLogController : Controller
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IAuditLogQueryService _queryService;

        public AuditLogController()
        {
            var connectionFactory = new SqlConnectionFactory();
            _auditLogRepository = new AuditLogRepository(connectionFactory);
            _queryService = new AuditLogQueryService(new AuditLogQueryRepository(connectionFactory));
        }

        public AuditLogController(IAuditLogRepository auditLogRepository, IAuditLogQueryService queryService)
        {
            _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        [HttpGet]
        public ActionResult Index(string searchTerm, string actionType, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            const int pageSize = 50;
            if (page < 1) page = 1;

            var rawEntries = _auditLogRepository.GetRecent(500) ?? Enumerable.Empty<AuditLogEntry>();
            var entriesList = rawEntries.ToList();

            var userIds = entriesList
                .Where(e => e.UserId.HasValue)
                .Select(e => e.UserId.Value.ToString())
                .Distinct()
                .OrderBy(s => s)
                .Take(15)
                .ToList();

            var entityTypes = entriesList
                .Where(e => !string.IsNullOrWhiteSpace(e.EntityType))
                .Select(e => e.EntityType)
                .Distinct()
                .OrderBy(s => s)
                .Take(15)
                .ToList();

            var entityIds = entriesList
                .Where(e => !string.IsNullOrWhiteSpace(e.EntityId))
                .Select(e => e.EntityId)
                .Distinct()
                .OrderBy(s => s)
                .Take(15)
                .ToList();

            var availableActions = entriesList
                .Select(e => e.Action)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .Select(a => new SelectListItem { Text = a, Value = a, Selected = (a == actionType) })
                .ToList();

            availableActions.Insert(0, new SelectListItem { Text = "-- All Action Types --", Value = "" });

            var filter = new AuditLogFilter
            {
                Username = searchTerm,
                Action = actionType,
                FromUtc = startDate,
                ToUtc = endDate.HasValue ? endDate.Value.AddDays(1) : (DateTime?)null
            };

            AuditLogPage pagedResult;
            try
            {
                pagedResult = _queryService.Search(filter, page, pageSize);
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                pagedResult = new AuditLogPage
                {
                    Items = new List<AuditLogEntry>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }

            var isAuth = User.Identity != null && User.Identity.IsAuthenticated &&
                         (User.IsInRole(RoleNames.Supervisor) || User.IsInRole(RoleNames.SystemAdmin));

            var model = new AuditLogViewModel
            {
                Entries = pagedResult.Items ?? Enumerable.Empty<AuditLogEntry>(),
                TotalCount = pagedResult.TotalCount,
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                ActionType = actionType,
                StartDate = startDate,
                EndDate = endDate,
                ActionTypes = availableActions,
                IsAuthorized = isAuth
            };

            ViewBag.UserIds = userIds;
            ViewBag.EntityTypes = entityTypes;
            ViewBag.EntityIds = entityIds;

            return View(model);
        }
    }
}