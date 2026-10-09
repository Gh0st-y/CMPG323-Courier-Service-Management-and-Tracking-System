using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourierService.Domain.Models
{
    public class PackageStatusHistoryItem
    {
        public string FromStatus { get; set; }
        public string ToStatus { get; set; }
        public string ChangedByUsername { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string Notes { get; set; }
    }
}