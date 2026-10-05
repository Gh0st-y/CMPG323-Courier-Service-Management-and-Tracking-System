using System;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;

namespace CourierService.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogger _auditLogger;

        public AuthService(IUserRepository userRepository, IAuditLogRepository auditLogRepository)
            : this(userRepository, new AuditLogger(auditLogRepository))
        {
        }

        public AuthService(IUserRepository userRepository, IAuditLogger auditLogger)
        {
            _userRepository = userRepository;
            _auditLogger = auditLogger;
        }

        public AuthResult Login(string username, string password)
        {
            var user = _userRepository.GetByUsername(username);

            if (user == null)
            {
                WriteAttempt(null, username, AuditActions.LoginFailed, "Unknown username");
                return AuthResult.Fail();
            }

            if (!user.IsActive)
            {
                WriteAttempt(user.UserId, username, AuditActions.LoginFailed, "Inactive account");
                return AuthResult.Fail();
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                WriteAttempt(user.UserId, username, AuditActions.LoginFailed, "Invalid password");
                return AuthResult.Fail();
            }

            _userRepository.UpdateLastLogin(user.UserId, DateTime.UtcNow);
            WriteAttempt(user.UserId, username, AuditActions.LoginSuccess, "Login succeeded");

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

        // The username is whatever the caller typed, so it can be longer than the EntityId column.
        // AuditLogger cuts it to fit, so an overlong username can't turn a failed login into a 500.
        private void WriteAttempt(int? userId, string username, string action, string detail)
        {
            _auditLogger.Log(action, AuditEntityTypes.User, username, detail, userId);
        }
    }
}