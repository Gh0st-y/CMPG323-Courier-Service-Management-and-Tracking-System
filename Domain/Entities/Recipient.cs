using System;

namespace CourierService.Domain.Entities
{
    /// <summary>Maps to dbo.Recipients. No dedicated repository (T05 scope) — read via Package.</summary>
    public class Recipient
    {
        public int RecipientId { get; set; }
        public string FullName { get; set; }
        public string IdentifierNo { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Department { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
