using System;
using System.Collections.Generic;
using System.Linq;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Auth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Auth
{
    [TestClass]
    public class AuthServiceTests
    {
        private sealed class FakeUserRepository : IUserRepository
        {
            public List<User> Users { get; } = new List<User>();
            public List<int> LastLoginUpdates { get; } = new List<int>();

            public User GetByUsername(string username)
            {
                return Users.FirstOrDefault(u => u.Username == username);
            }

            public User GetById(int userId)
            {
                return Users.FirstOrDefault(u => u.UserId == userId);
            }

            public int Insert(User user, IUnitOfWork unitOfWork = null)
            {
                Users.Add(user);
                return user.UserId;
            }

            public void UpdateLastLogin(int userId, DateTime loginTimeUtc, IUnitOfWork unitOfWork = null)
            {
                LastLoginUpdates.Add(userId);
            }
        }

        // Enforces the same column sizes as dbo.AuditLog, like SQL Server does, so an overlong value fails here too
        private sealed class StrictFakeAuditLogRepository : IAuditLogRepository
        {
            public List<AuditLogEntry> Entries { get; } = new List<AuditLogEntry>();

            public void Insert(AuditLogEntry entry, IUnitOfWork unitOfWork = null)
            {
                if (entry.EntityId != null && entry.EntityId.Length > AuditLogger.MaxEntityIdLength)
                {
                    throw new InvalidOperationException("String or binary data would be truncated (EntityId).");
                }

                Entries.Add(entry);
            }

            public IEnumerable<AuditLogEntry> GetRecent(int take)
            {
                return Entries.Take(take);
            }
        }

        private FakeUserRepository _users;
        private StrictFakeAuditLogRepository _audit;
        private AuthService _service;

        [TestInitialize]
        public void Setup()
        {
            _users = new FakeUserRepository();
            _users.Users.Add(new User
            {
                UserId = 7,
                Username = "intake.demo",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@2026!", 4),
                RoleName = "IntakeClerk",
                IsActive = true
            });
            _audit = new StrictFakeAuditLogRepository();
            _service = new AuthService(_users, _audit);
        }

        [TestMethod]
        public void Login_CorrectPassword_Succeeds_AndAuditsLoginSuccess()
        {
            var result = _service.Login("intake.demo", "Demo@2026!");

            Assert.IsTrue(result.Success);
            Assert.AreEqual("IntakeClerk", result.RoleName);
            CollectionAssert.AreEqual(new[] { 7 }, _users.LastLoginUpdates);
            Assert.AreEqual(AuditActions.LoginSuccess, _audit.Entries.Single().Action);
            Assert.AreEqual(7, _audit.Entries.Single().UserId);
        }

        [TestMethod]
        public void Login_WrongPassword_Fails_AndAuditsLoginFailed()
        {
            var result = _service.Login("intake.demo", "not-the-password");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, _users.LastLoginUpdates.Count);
            Assert.AreEqual(AuditActions.LoginFailed, _audit.Entries.Single().Action);
        }

        [TestMethod]
        public void Login_UnknownUser_Fails_AndAuditsWithNoUserId()
        {
            var result = _service.Login("nobody", "x");

            Assert.IsFalse(result.Success);
            Assert.IsNull(_audit.Entries.Single().UserId);
        }

        [TestMethod]
        public void Login_InactiveUser_Fails_EvenWithCorrectPassword()
        {
            _users.Users[0].IsActive = false;

            var result = _service.Login("intake.demo", "Demo@2026!");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AuditActions.LoginFailed, _audit.Entries.Single().Action);
        }

        [TestMethod]
        public void Login_MalformedStoredHash_Fails_InsteadOfThrowing()
        {
            _users.Users[0].PasswordHash = "not-a-bcrypt-hash";

            var result = _service.Login("intake.demo", "Demo@2026!");

            Assert.IsFalse(result.Success);
        }

        // Regression: a 100+ character username used to make the audit insert throw, which
        // surfaced as an unhandled 500 with a stack trace instead of a normal 401.
        [TestMethod]
        public void Login_OverlongUsername_FailsNormally_AndTheAuditEntryFitsTheColumn()
        {
            var longUsername = new string('a', 500);

            var result = _service.Login(longUsername, "x");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AuditLogger.MaxEntityIdLength, _audit.Entries.Single().EntityId.Length);
        }
    }
}
