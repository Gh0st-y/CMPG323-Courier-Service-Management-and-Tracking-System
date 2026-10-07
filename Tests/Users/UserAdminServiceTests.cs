using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Security;
using CourierService.Services.Users;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Users
{
    [TestClass]
    public class UserAdminServiceTests
    {
        // One in-memory user table shared by both fake repositories, like dbo.Users is
        private sealed class UserStore
        {
            public static readonly string[] Roles =
            {
                RoleNames.IntakeClerk, RoleNames.StorageStaff, RoleNames.CollectionStaff, RoleNames.Supervisor, RoleNames.SystemAdmin
            };

            public List<User> Users { get; } = new List<User>();

            public User Add(string username, string role, bool isActive = true)
            {
                var user = new User
                {
                    UserId = Users.Count + 1,
                    Username = username,
                    Email = username + "@courier.test",
                    PasswordHash = "existing-hash",
                    RoleId = Array.IndexOf(Roles, role) + 1,
                    RoleName = role,
                    IsActive = isActive
                };
                Users.Add(user);
                return user;
            }
        }

        private sealed class FakeUserRepository : IUserRepository
        {
            private readonly UserStore _store;

            public FakeUserRepository(UserStore store)
            {
                _store = store;
            }

            // SQL Server's default collation ignores case, so this does too
            public User GetByUsername(string username)
            {
                return _store.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            }

            public User GetById(int userId)
            {
                return _store.Users.FirstOrDefault(u => u.UserId == userId);
            }

            public int Insert(User user, IUnitOfWork unitOfWork = null)
            {
                user.UserId = _store.Users.Count + 1;
                _store.Users.Add(user);
                return user.UserId;
            }

            public void UpdateLastLogin(int userId, DateTime loginTimeUtc, IUnitOfWork unitOfWork = null)
            {
            }
        }

        private sealed class FakeUserAdminRepository : IUserAdminRepository
        {
            private readonly UserStore _store;

            public FakeUserAdminRepository(UserStore store)
            {
                _store = store;
            }

            public IList<User> GetAll()
            {
                return _store.Users.OrderBy(u => u.Username).ToList();
            }

            public int? GetRoleId(string roleName)
            {
                var index = Array.IndexOf(UserStore.Roles, roleName);
                return index < 0 ? (int?)null : index + 1;
            }

            public bool EmailExists(string email)
            {
                return _store.Users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            }

            public int CountActiveUsersInRole(string roleName, IUnitOfWork unitOfWork = null)
            {
                return _store.Users.Count(u => u.IsActive && u.RoleName == roleName);
            }

            public void UpdateRoleAndStatus(int userId, int roleId, bool isActive, IUnitOfWork unitOfWork = null)
            {
                var user = _store.Users.Single(u => u.UserId == userId);
                user.RoleId = roleId;
                user.RoleName = UserStore.Roles[roleId - 1];
                user.IsActive = isActive;
            }
        }

        private sealed class AuditCall
        {
            public string Action;
            public string EntityType;
            public string EntityId;
            public string Detail;
            public int? UserId;
        }

        private sealed class FakeAuditLogger : IAuditLogger
        {
            public List<AuditCall> Calls { get; } = new List<AuditCall>();

            public void Log(string action, string entityType, string entityId, string detail, int? userId = null, IUnitOfWork unitOfWork = null)
            {
                Calls.Add(new AuditCall { Action = action, EntityType = entityType, EntityId = entityId, Detail = detail, UserId = userId });
            }
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public bool Committed { get; private set; }
            public IDbConnection Connection => null;
            public IDbTransaction Transaction => null;

            public void Commit()
            {
                Committed = true;
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeUnitOfWorkFactory : IUnitOfWorkFactory
        {
            public List<FakeUnitOfWork> Started { get; } = new List<FakeUnitOfWork>();

            public IUnitOfWork Begin()
            {
                var unitOfWork = new FakeUnitOfWork();
                Started.Add(unitOfWork);
                return unitOfWork;
            }
        }

        private UserStore _store;
        private FakeAuditLogger _audit;
        private FakeUnitOfWorkFactory _unitOfWork;
        private UserAdminService _service;
        private User _admin;

        [TestInitialize]
        public void SetUp()
        {
            _store = new UserStore();
            _audit = new FakeAuditLogger();
            _unitOfWork = new FakeUnitOfWorkFactory();
            _service = new UserAdminService(
                new FakeUserRepository(_store),
                new FakeUserAdminRepository(_store),
                _audit,
                _unitOfWork,
                p => "hashed:" + p);

            _admin = _store.Add("admin.demo", RoleNames.SystemAdmin);
        }

        // ---- Create ----

        [TestMethod]
        public void Create_SavesAnActiveUserWithAHashedPasswordAndTheRole()
        {
            var result = _service.Create("new.clerk", "new.clerk@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.Created, result.Outcome);
            var saved = _store.Users.Single(u => u.Username == "new.clerk");
            Assert.AreEqual("hashed:Welcome123", saved.PasswordHash);
            Assert.AreNotEqual("Welcome123", saved.PasswordHash);
            Assert.IsTrue(saved.IsActive);
            Assert.AreEqual(RoleNames.IntakeClerk, saved.RoleName);
            Assert.AreEqual(1, saved.RoleId);
        }

        [TestMethod]
        public void Create_WritesOneAuditEntryWithIdsAndRoleOnly_AndCommits()
        {
            var result = _service.Create("new.clerk", "new.clerk@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId);

            Assert.AreEqual(1, _audit.Calls.Count);
            var call = _audit.Calls[0];
            Assert.AreEqual(AuditActions.UserCreated, call.Action);
            Assert.AreEqual(AuditEntityTypes.User, call.EntityType);
            Assert.AreEqual(result.User.UserId.ToString(), call.EntityId);
            Assert.AreEqual("Role: IntakeClerk", call.Detail);
            Assert.AreEqual(_admin.UserId, call.UserId);
            Assert.IsFalse(call.Detail.Contains("@"), "No email in the audit log (SR-03)");
            Assert.IsTrue(_unitOfWork.Started.Single().Committed);
        }

        [TestMethod]
        public void Create_AcceptsAnyRoleCasing_AndStoresTheExactRoleName()
        {
            var result = _service.Create("new.super", "new.super@courier.test", "Welcome123", "supervisor", _admin.UserId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(RoleNames.Supervisor, result.User.RoleName);
        }

        [TestMethod]
        public void Create_TrimsUsernameAndEmail()
        {
            var result = _service.Create("  new.clerk ", " new.clerk@courier.test ", "Welcome123", RoleNames.IntakeClerk, _admin.UserId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("new.clerk", result.User.Username);
            Assert.AreEqual("new.clerk@courier.test", result.User.Email);
        }

        [TestMethod]
        public void Create_WithAWeakPassword_IsRejectedAndNothingIsSaved()
        {
            var result = _service.Create("new.clerk", "new.clerk@courier.test", "password", RoleNames.IntakeClerk, _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.ValidationError, result.Outcome);
            Assert.AreEqual(PasswordPolicy.Requirement, result.Message);
            Assert.AreEqual(1, _store.Users.Count);
            Assert.AreEqual(0, _audit.Calls.Count);
        }

        [TestMethod]
        public void Create_WithATakenUsername_IsAConflict_EvenInDifferentCase()
        {
            var result = _service.Create("ADMIN.demo", "someone.else@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.Conflict, result.Outcome);
            Assert.AreEqual("UsernameTaken", result.ErrorCode);
            Assert.AreEqual(1, _store.Users.Count);
        }

        [TestMethod]
        public void Create_WithATakenEmail_IsAConflict()
        {
            var result = _service.Create("someone.else", "admin.demo@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.Conflict, result.Outcome);
            Assert.AreEqual("EmailTaken", result.ErrorCode);
        }

        [TestMethod]
        public void Create_WithAnUnknownRole_IsRejected()
        {
            var result = _service.Create("new.user", "new.user@courier.test", "Welcome123", "Manager", _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.ValidationError, result.Outcome);
            Assert.IsTrue(result.Message.Contains("SystemAdmin"), "The message lists the allowed roles");
        }

        [TestMethod]
        public void Create_WithMissingFields_IsRejected()
        {
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("", "a@b.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId).Outcome);
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("new.user", null, "Welcome123", RoleNames.IntakeClerk, _admin.UserId).Outcome);
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("new.user", "a@b.test", null, RoleNames.IntakeClerk, _admin.UserId).Outcome);
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("new.user", "a@b.test", "Welcome123", " ", _admin.UserId).Outcome);
        }

        [TestMethod]
        public void Create_WithABadUsernameOrEmail_IsRejected()
        {
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("ab", "ab@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId).Outcome, "too short");
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("has space", "x@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId).Outcome, "space");
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("<script>", "y@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId).Outcome, "markup");
            Assert.AreEqual(UserAdminOutcome.ValidationError,
                _service.Create("new.user", "not-an-email", "Welcome123", RoleNames.IntakeClerk, _admin.UserId).Outcome, "email");
        }

        [TestMethod]
        public void Create_WithTheRealHasher_StoresABCryptHashThatVerifies()
        {
            var service = new UserAdminService(
                new FakeUserRepository(_store), new FakeUserAdminRepository(_store), _audit, _unitOfWork);

            var result = service.Create("bcrypt.user", "bcrypt.user@courier.test", "Welcome123", RoleNames.IntakeClerk, _admin.UserId);

            var hash = _store.Users.Single(u => u.Username == "bcrypt.user").PasswordHash;
            Assert.IsTrue(result.Success);
            Assert.IsTrue(hash.StartsWith("$2"), "A BCrypt hash starts with $2a$, $2b$ or $2y$");
            Assert.IsTrue(hash.Contains("$11$"), "Work factor 11, the same as the seeded users");
            Assert.IsTrue(BCrypt.Net.BCrypt.Verify("Welcome123", hash));
            Assert.IsFalse(BCrypt.Net.BCrypt.Verify("Welcome124", hash));
        }

        // ---- Update ----

        [TestMethod]
        public void Update_ChangesTheRole_AndAuditsFromAndTo()
        {
            var clerk = _store.Add("clerk", RoleNames.IntakeClerk);

            var result = _service.Update(clerk.UserId, RoleNames.Supervisor, null, _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.Ok, result.Outcome);
            Assert.AreEqual(RoleNames.Supervisor, result.User.RoleName);
            Assert.AreEqual(4, result.User.RoleId);
            var call = _audit.Calls.Single();
            Assert.AreEqual(AuditActions.UserRoleChanged, call.Action);
            Assert.AreEqual("Role: IntakeClerk -> Supervisor", call.Detail);
            Assert.AreEqual(clerk.UserId.ToString(), call.EntityId);
            Assert.IsTrue(_unitOfWork.Started.Single().Committed);
        }

        [TestMethod]
        public void Update_Deactivates_AndReactivates()
        {
            var clerk = _store.Add("clerk", RoleNames.IntakeClerk);

            Assert.IsFalse(_service.Update(clerk.UserId, null, false, _admin.UserId).User.IsActive);
            Assert.IsTrue(_service.Update(clerk.UserId, null, true, _admin.UserId).User.IsActive);

            CollectionAssert.AreEqual(
                new[] { AuditActions.UserDeactivated, AuditActions.UserReactivated },
                _audit.Calls.Select(c => c.Action).ToArray());
        }

        [TestMethod]
        public void Update_RoleAndStatusTogether_WritesTwoAuditEntriesInOneTransaction()
        {
            var clerk = _store.Add("clerk", RoleNames.IntakeClerk);

            var result = _service.Update(clerk.UserId, RoleNames.StorageStaff, false, _admin.UserId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, _audit.Calls.Count);
            Assert.AreEqual(1, _unitOfWork.Started.Count);
        }

        [TestMethod]
        public void Update_ThatChangesNothing_IsOk_AndWritesNothing()
        {
            var clerk = _store.Add("clerk", RoleNames.IntakeClerk);

            var result = _service.Update(clerk.UserId, "intakeclerk", true, _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.Ok, result.Outcome);
            Assert.AreEqual(0, _audit.Calls.Count);
            Assert.AreEqual(0, _unitOfWork.Started.Count);
        }

        [TestMethod]
        public void Update_WithNothingToChange_IsRejected()
        {
            var clerk = _store.Add("clerk", RoleNames.IntakeClerk);

            Assert.AreEqual(UserAdminOutcome.ValidationError, _service.Update(clerk.UserId, null, null, _admin.UserId).Outcome);
        }

        [TestMethod]
        public void Update_WithAnUnknownRole_IsRejected()
        {
            var clerk = _store.Add("clerk", RoleNames.IntakeClerk);

            Assert.AreEqual(UserAdminOutcome.ValidationError, _service.Update(clerk.UserId, "Manager", null, _admin.UserId).Outcome);
            Assert.AreEqual(RoleNames.IntakeClerk, clerk.RoleName);
        }

        [TestMethod]
        public void Update_UnknownUser_IsNotFound()
        {
            Assert.AreEqual(UserAdminOutcome.NotFound, _service.Update(999, null, false, _admin.UserId).Outcome);
        }

        [TestMethod]
        public void Update_YourOwnAccount_IsRefused()
        {
            var result = _service.Update(_admin.UserId, null, false, _admin.UserId);

            Assert.AreEqual(UserAdminOutcome.ValidationError, result.Outcome);
            Assert.AreEqual("OwnAccount", result.ErrorCode);
            Assert.IsTrue(_admin.IsActive);
            Assert.AreEqual(0, _audit.Calls.Count);
        }

        [TestMethod]
        public void Update_RemovingTheLastActiveSystemAdmin_IsAConflict()
        {
            // A second admin does the change, but the target is the only *active* System Admin left
            var otherAdmin = _store.Add("other.admin", RoleNames.SystemAdmin, isActive: false);
            var supervisor = _store.Add("boss", RoleNames.Supervisor);

            var result = _service.Update(_admin.UserId, RoleNames.Supervisor, null, supervisor.UserId);

            Assert.AreEqual(UserAdminOutcome.Conflict, result.Outcome);
            Assert.AreEqual("LastAdministrator", result.ErrorCode);
            Assert.AreEqual(RoleNames.SystemAdmin, _admin.RoleName);
            Assert.IsFalse(otherAdmin.IsActive);
            Assert.IsFalse(_unitOfWork.Started.Single().Committed, "Refused, so the transaction is rolled back");
        }

        [TestMethod]
        public void Update_DeactivatingAnAdmin_IsAllowed_WhenAnotherActiveAdminRemains()
        {
            var secondAdmin = _store.Add("second.admin", RoleNames.SystemAdmin);

            var result = _service.Update(secondAdmin.UserId, null, false, _admin.UserId);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(secondAdmin.IsActive);
        }

        [TestMethod]
        public void GetAll_ReturnsEveryUser()
        {
            _store.Add("zed", RoleNames.IntakeClerk, isActive: false);
            _store.Add("bob", RoleNames.StorageStaff);

            CollectionAssert.AreEqual(
                new[] { "admin.demo", "bob", "zed" },
                _service.GetAll().Select(u => u.Username).ToArray());
        }
    }
}