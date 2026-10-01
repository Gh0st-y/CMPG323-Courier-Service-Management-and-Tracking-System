using System;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public AuthService(IUserRepository userRepository, IAuditLogRepository auditLogRepository)
        {
            _userRepository = userRepository;
            _auditLogRepository = auditLogRepository;
        }

        public AuthResult Login(string username, string password)
        {
            var user = _userRepository.GetByUsername(username);

            if (user == null)
            {
                WriteAttempt(null, username, "LoginFailed", "Unknown username");
                return AuthResult.Fail();
            }

            if (!user.IsActive)
            {
                WriteAttempt(user.UserId, username, "LoginFailed", "Inactive account");
                return AuthResult.Fail();
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                WriteAttempt(user.UserId, username, "LoginFailed", "Invalid password");
                return AuthResult.Fail();
            }

            _userRepository.UpdateLastLogin(user.UserId, DateTime.UtcNow);
            WriteAttempt(user.UserId, username, "LoginSuccess", "Login succeeded");

            return AuthResult.Ok(user.UserId, user.Username, user.RoleName);
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        private void WriteAttempt(int? userId, string username, string action, string detail)
        {
            _auditLogRepository.Insert(new AuditLogEntry
            {
                UserId = userId,
                Action = action,
                EntityType = "User",
                EntityId = username,
                Detail = detail,
                OccurredAtUtc = DateTime.UtcNow
            });
        }
    }
}