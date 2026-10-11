using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CourierService.Domain.Entities;

namespace CourierService.Web.Models
{
    public class AuditLogViewModel
    {
        public IEnumerable<AuditLogEntry> Entries { get; set; }

        // Pagination Properties
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }

        // Filter Form Properties
        public string SearchTerm { get; set; }
        public string ActionType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Dropdowns & Suggestions
        public IEnumerable<SelectListItem> ActionTypes { get; set; }

        public IEnumerable<SelectListItem> KeywordGroups { get; set; }
        public IEnumerable<string> SearchSuggestions { get; set; }

        // Authorization Status
        public bool IsAuthorized { get; set; }
    }
}