using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourierService.Domain.Models
{
    public class NotificationLogItem
    {
        public string Channel { get; set; }
        public string RecipientAddress { get; set; }
        public string Subject { get; set; }
        public string Status { get; set; }
        public DateTime SentAtUtc { get; set; }
    }
}
