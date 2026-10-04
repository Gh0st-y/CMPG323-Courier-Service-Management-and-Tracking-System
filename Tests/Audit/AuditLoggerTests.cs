using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Audit
{
    [TestClass]
    public class AuditLoggerTests
    {
        // Fake repo that just remembers what it was given
        private sealed class FakeAuditLogRepository : IAuditLogRepository
        {
            public List<AuditLogEntry> Entries { get; } = new List<AuditLogEntry>();
            public List<IUnitOfWork> UnitsOfWork { get; } = new List<IUnitOfWork>();

            public void Insert(AuditLogEntry entry, IUnitOfWork unitOfWork = null)
            {
                Entries.Add(entry);
                UnitsOfWork.Add(unitOfWork);
            }

            public IEnumerable<AuditLogEntry> GetRecent(int take)
            {
                return Entries.Take(take);
            }
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public IDbConnection Connection { get { return null; } }
            public IDbTransaction Transaction { get { return null; } }
            public void Commit() { }
            public void Dispose() { }
        }

        private FakeAuditLogRepository _repository;
        private AuditLogger _logger;

        [TestInitialize]
        public void Setup()
        {
            _repository = new FakeAuditLogRepository();
            _logger = new AuditLogger(_repository);
        }

        [TestMethod]
        public void Log_WritesAllFieldsToRepository()
        {
            var before = DateTime.UtcNow;

            _logger.Log(AuditActions.PackageCreated, AuditEntityTypes.Package, "F20-0001", "Registered at intake", 7);

            Assert.AreEqual(1, _repository.Entries.Count);
            var entry = _repository.Entries[0];
            Assert.AreEqual(AuditActions.PackageCreated, entry.Action);
            Assert.AreEqual(AuditEntityTypes.Package, entry.EntityType);
            Assert.AreEqual("F20-0001", entry.EntityId);
            Assert.AreEqual("Registered at intake", entry.Detail);
            Assert.AreEqual(7, entry.UserId);
            Assert.IsTrue(entry.OccurredAtUtc >= before && entry.OccurredAtUtc <= DateTime.UtcNow);
        }

        [TestMethod]
        public void Log_PassesCallersUnitOfWorkThrough()
        {
            var unitOfWork = new FakeUnitOfWork();

            _logger.Log(AuditActions.PackageStatusChanged, AuditEntityTypes.Package, "F20-0001", "InStorage to ReadyForCollection", 3, unitOfWork);

            Assert.AreSame(unitOfWork, _repository.UnitsOfWork[0]);
        }

        [TestMethod]
        public void Log_WithoutUnitOfWork_PassesNull()
        {
            _logger.Log(AuditActions.ConfigChanged, AuditEntityTypes.Config, "Smtp.Host", "Updated", 1);

            Assert.IsNull(_repository.UnitsOfWork[0]);
        }

        [TestMethod]
        public void Log_AllowsNoUser_ForFailedLoginOfUnknownUsername()
        {
            _logger.Log(AuditActions.LoginFailed, AuditEntityTypes.User, "nobody", "Unknown username");

            Assert.IsNull(_repository.Entries[0].UserId);
        }

        [TestMethod]
        public void Log_BlankAction_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => _logger.Log(null, "User", "x", "y"));
            Assert.ThrowsExactly<ArgumentException>(() => _logger.Log("", "User", "x", "y"));
            Assert.ThrowsExactly<ArgumentException>(() => _logger.Log("   ", "User", "x", "y"));
            Assert.AreEqual(0, _repository.Entries.Count);
        }

        [TestMethod]
        public void Log_ActionLongerThanColumn_Throws()
        {
            var tooLong = new string('a', AuditLogger.MaxActionLength + 1);

            Assert.ThrowsExactly<ArgumentException>(() => _logger.Log(tooLong, "User", "x", "y"));
            Assert.AreEqual(0, _repository.Entries.Count);
        }

        [TestMethod]
        public void Log_TruncatesOverlongFreeTextInsteadOfFailing()
        {
            _logger.Log(
                AuditActions.CsvImported,
                new string('t', 200),
                new string('i', 200),
                new string('d', 5000));

            var entry = _repository.Entries[0];
            Assert.AreEqual(AuditLogger.MaxEntityTypeLength, entry.EntityType.Length);
            Assert.AreEqual(AuditLogger.MaxEntityIdLength, entry.EntityId.Length);
            Assert.AreEqual(AuditLogger.MaxDetailLength, entry.Detail.Length);
        }

        [TestMethod]
        public void Log_KeepsNullOptionalFieldsNull()
        {
            _logger.Log(AuditActions.ConfigChanged, null, null, null);

            var entry = _repository.Entries[0];
            Assert.IsNull(entry.EntityType);
            Assert.IsNull(entry.EntityId);
            Assert.IsNull(entry.Detail);
        }

        [TestMethod]
        public void Constructor_NullRepository_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new AuditLogger(null));
        }

        [TestMethod]
        public void AuditLog_IsAppendOnly_NoUpdateOrDeleteMembersAnywhere()
        {
            var forbiddenPrefixes = new[] { "Update", "Delete", "Remove", "Edit", "Modify", "Clear", "Purge" };
            var types = new[] { typeof(IAuditLogger), typeof(AuditLogger), typeof(IAuditLogRepository) };

            foreach (var type in types)
            {
                var offending = type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(m => m.Name)
                    .Where(name => forbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                Assert.AreEqual(0, offending.Count,
                    type.Name + " exposes a mutating method: " + string.Join(", ", offending));
            }
        }

        [TestMethod]
        public void AuditActions_AreUniqueAndFitTheActionColumn()
        {
            var values = typeof(AuditActions)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();

            Assert.IsTrue(values.Count > 0);
            Assert.AreEqual(values.Count, values.Distinct().Count(), "Duplicate action names.");
            Assert.IsTrue(values.All(v => !string.IsNullOrWhiteSpace(v) && v.Length <= AuditLogger.MaxActionLength));
        }
    }
}