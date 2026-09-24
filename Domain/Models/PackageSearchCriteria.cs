using System;
using CourierService.Domain.Entities;

namespace CourierService.Domain.Models
{
    /// <summary>Filters for package search (T21). Any filter left null/empty is ignored.</summary>
    public class PackageSearchCriteria
    {
        /// <summary>One search box: matches package ID, recipient ID number, or recipient name.</summary>
        public string Query { get; set; }
        public DateTime? ReceivedFrom { get; set; }
        public DateTime? ReceivedTo { get; set; }
        public PackageStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}