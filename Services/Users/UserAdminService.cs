using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Security;

namespace CourierService.Services.Users
{
    public class UserAdminService : IUserAdminService
    {
        // Same cost as the seeded demo users ($2b$11$...), so new and seeded logins take about the same time
        public const int BCryptWorkFactor = 11;

        // Matches dbo.Users.Email
        public const int MaxEmailLength = 200;

        // Letters, numbers, dots, dashes and underscores, 3 to 100 characters (dbo.Users.Username is NVARCHAR(100))
        private static readonly Regex UsernamePattern = new Regex(@"^[A-Za-z0-9._-]{3,100}$");

        // A sanity check, not full RFC validation: something@something.something with no spaces
        private static readonly Regex EmailPattern = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        private static readonly string[] AllRoles =
        {
            RoleNames.IntakeClerk, RoleNames.StorageStaff, RoleNames.CollectionStaff, RoleNames.Supervisor, RoleNames.SystemAdmin
        };

        private readonly IUserRepository _users;
        private readonly IUserAdminRepository _admin;
        private readonly IAuditLogger _auditLogger;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;
        private readonly Func<string, string> _hashPassword;

        public UserAdminService(
            IUserRepository users,
            IUserAdminRepository admin,
            IAuditLogger auditLogger,
            IUnitOfWorkFactory unitOfWorkFactory,
            Func<string, string> hashPassword = null)
        {
            if (users == null) throw new ArgumentNullException(nameof(users));
            if (admin == null) throw new ArgumentNullException(nameof(admin));
            if (auditLogger == null) throw new ArgumentNullException(nameof(auditLogger));
            if (unitOfWorkFactory == null) throw new ArgumentNullException(nameof(unitOfWorkFactory));

            _users = users;
            _admin = admin;
            _auditLogger = auditLogger;
            _unitOfWorkFactory = unitOfWorkFactory;

            // Tests can pass a cheap stand-in. The real thing is BCrypt (NRF-013, DECISIONS.md auth choice).
            _hashPassword = hashPassword ?? (p => BCrypt.Net.BCrypt.HashPassword(p, BCryptWorkFactor));
        }

        public static string AllowedRolesText => string.Join(", ", AllRoles);

        public IList<User> GetAll()
        {
            return _admin.GetAll();
        }

        public UserAdminResult Create(string username, string email, string password, string role, int actingUserId)
        {
            username = (username ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim();

            if (username.Length == 0 || email.Length == 0 || string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(role))
            {
                return UserAdminResult.Invalid("username, email, password and role are all required.");
            }

            if (!UsernamePattern.IsMatch(username))
            {
                return UserAdminResult.Invalid(
                    "Username must be 3 to 100 characters and use only letters, numbers, dots, dashes or underscores.");
            }

            if (email.Length > MaxEmailLength || !EmailPattern.IsMatch(email))
            {
                return UserAdminResult.Invalid("That email address doesn't look valid.");
            }

            string roleName;
            if (!TryNormalizeRole(role, out roleName))
            {
                return UserAdminResult.Invalid("role must be one of: " + AllowedRolesText + ".");
            }

            string passwordMessage;
            if (!PasswordPolicy.IsValid(password, out passwordMessage))
            {
                return UserAdminResult.Invalid(passwordMessage);
            }

            // Checked up front for a friendly 409. The UNIQUE constraints in dbo.Users still catch a race.
            if (_users.GetByUsername(username) != null)
            {
                return UserAdminResult.Conflict("UsernameTaken", "That username is already in use.");
            }

            if (_admin.EmailExists(email))
            {
                return UserAdminResult.Conflict("EmailTaken", "That email address is already in use.");
            }

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = _hashPassword(password),
                RoleId = RequireRoleId(roleName),
                RoleName = roleName,
                IsActive = true
            };

            // The user and its audit entry are written together or not at all
            using (var unitOfWork = _unitOfWorkFactory.Begin())
            {
                user.UserId = _users.Insert(user, unitOfWork);

                // Ids and role only. No username or email in the audit detail (SR-03).
                _auditLogger.Log(
                    AuditActions.UserCreated,
                    AuditEntityTypes.User,
                    user.UserId.ToString(CultureInfo.InvariantCulture),
                    "Role: " + roleName,
                    actingUserId,
                    unitOfWork);

                unitOfWork.Commit();
            }

            return UserAdminResult.Created(user);
        }

        public UserAdminResult Update(int userId, string role, bool? isActive, int actingUserId)
        {
            if (role == null && !isActive.HasValue)
            {
                return UserAdminResult.Invalid("Send role, isActive or both.");
            }

            string newRole = null;
            if (role != null && !TryNormalizeRole(role, out newRole))
            {
                return UserAdminResult.Invalid("role must be one of: " + AllowedRolesText + ".");
            }

            var user = _users.GetById(userId);
            if (user == null)
            {
                return UserAdminResult.NotFound();
            }

            // Kept before anything is written, for the audit entry
            var oldRole = user.RoleName;
            var targetRole = newRole ?? user.RoleName;
            var targetActive = isActive ?? user.IsActive;
            var roleChanges = !string.Equals(targetRole, user.RoleName, StringComparison.Ordinal);
            var activeChanges = targetActive != user.IsActive;

            // Asking for what is already there is fine, and writes nothing
            if (!roleChanges && !activeChanges)
            {
                return UserAdminResult.Ok(user);
            }

            // An admin can't lock themselves out by accident. Another admin has to do it.
            if (userId == actingUserId)
            {
                return UserAdminResult.Invalid(
                    "You can't change your own role or deactivate your own account. Ask another System Admin.",
                    "OwnAccount");
            }

            var roleId = roleChanges ? RequireRoleId(targetRole) : user.RoleId;

            using (var unitOfWork = _unitOfWorkFactory.Begin())
            {
                var removesAnAdmin = user.IsActive
                    && string.Equals(user.RoleName, RoleNames.SystemAdmin, StringComparison.Ordinal)
                    && (!targetActive || !string.Equals(targetRole, RoleNames.SystemAdmin, StringComparison.Ordinal));

                // Leaving without Commit() rolls back, so nothing is written for a refused change
                if (removesAnAdmin && _admin.CountActiveUsersInRole(RoleNames.SystemAdmin, unitOfWork) <= 1)
                {
                    return UserAdminResult.Conflict(
                        "LastAdministrator",
                        "This is the only active System Admin. Make another user a System Admin first.");
                }

                _admin.UpdateRoleAndStatus(userId, roleId, targetActive, unitOfWork);

                var entityId = userId.ToString(CultureInfo.InvariantCulture);

                if (roleChanges)
                {
                    _auditLogger.Log(AuditActions.UserRoleChanged, AuditEntityTypes.User, entityId,
                        "Role: " + oldRole + " -> " + targetRole, actingUserId, unitOfWork);
                }

                if (activeChanges)
                {
                    _auditLogger.Log(targetActive ? AuditActions.UserReactivated : AuditActions.UserDeactivated,
                        AuditEntityTypes.User, entityId,
                        targetActive ? "Account reactivated" : "Account deactivated", actingUserId, unitOfWork);
                }

                unitOfWork.Commit();
            }

            return UserAdminResult.Ok(_users.GetById(userId));
        }

        // Accepts any casing ("supervisor") and hands back the exact name stored in dbo.Roles ("Supervisor")
        private static bool TryNormalizeRole(string role, out string roleName)
        {
            roleName = AllRoles.FirstOrDefault(r => string.Equals(r, (role ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            return roleName != null;
        }

        private int RequireRoleId(string roleName)
        {
            var roleId = _admin.GetRoleId(roleName);
            if (!roleId.HasValue)
            {
                // Only happens if dbo.Roles wasn't seeded, which is a setup problem and not the caller's fault
                throw new InvalidOperationException("dbo.Roles has no row for " + roleName + ". Run db/seed.sql.");
            }

            return roleId.Value;
        }
    }
}