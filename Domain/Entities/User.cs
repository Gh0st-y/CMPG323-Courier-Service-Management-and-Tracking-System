using System;

namespace CourierService.Domain.Entities
{
    /// <summary>Maps to dbo.Users. PasswordHash is a BCrypt hash (DECISIONS.md auth choice) — never plaintext.</summary>
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }
}
