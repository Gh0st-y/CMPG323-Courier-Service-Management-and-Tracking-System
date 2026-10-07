using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourierService.Domain.Models
{
    public class DashboardStats
    {
        public int ReceivedToday { get; set; }
        public int ReadyForCollection { get; set; }
        public int CollectedToday { get; set; }
        public int Outstanding { get; set; }
    }
}
