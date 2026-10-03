using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CourierService.Domain.Entities;

namespace CourierService.Web.Models
{
    public class AuditLogViewModel
    {
        public IEnumerable<AuditLogEntry> Entries { get; set; }

        // Filter Properties
        public string SearchTerm { get; set; }
        public string ActionType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Dropdown for Action Type Filter
        public IEnumerable<SelectListItem> ActionTypes { get; set; }

        // Auto-complete suggestions for Search Keywords
        public IEnumerable<string> SearchSuggestions { get; set; }

        // Authorization status flag
        public bool IsAuthorized { get; set; }
    }
}