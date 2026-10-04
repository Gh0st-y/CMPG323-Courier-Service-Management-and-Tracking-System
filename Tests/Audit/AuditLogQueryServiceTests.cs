using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Audit
{
    [TestClass]
    public class AuditLogQueryServiceTests
    {
        // Fake repo that remembers what it was asked for
        private sealed class FakeQueryRepository : IAuditLogQueryRepository
        {
            public AuditLogFilter LastFilter { get; private set; }
            public int LastPage { get; private set; }
            public int LastPageSize { get; private set; }
            public int Calls { get; private set; }

            public AuditLogPage Search(AuditLogFilter filter, int page, int pageSize)
            {
                Calls++;
                LastFilter = filter;
                LastPage = page;
                LastPageSize = pageSize;
                return new AuditLogPage { Items = new List<AuditLogEntry>(), TotalCount = 42 };
            }
        }

        private FakeQueryRepository _repository;
        private AuditLogQueryService _service;

        [TestInitialize]
        public void Setup()
        {
            _repository = new FakeQueryRepository();
            _service = new AuditLogQueryService(_repository);
        }

        [TestMethod]
        public void Search_PassesFilterAndPagingToRepository()
        {
            var from = new DateTime(2026, 9, 1);
            var to = new DateTime(2026, 10, 1);

            _service.Search(new AuditLogFilter { Username = "admin.demo", Action = "LoginFailed", FromUtc = from, ToUtc = to }, 3, 10);

            Assert.AreEqual("admin.demo", _repository.LastFilter.Username);
            Assert.AreEqual("LoginFailed", _repository.LastFilter.Action);
            Assert.AreEqual(from, _repository.LastFilter.FromUtc);
            Assert.AreEqual(to, _repository.LastFilter.ToUtc);
            Assert.AreEqual(3, _repository.LastPage);
            Assert.AreEqual(10, _repository.LastPageSize);
        }

        [TestMethod]
        public void Search_ReturnsTotalCountAndSetsPageInfo()
        {
            var result = _service.Search(new AuditLogFilter(), 2, 10);

            Assert.AreEqual(42, result.TotalCount);
            Assert.AreEqual(2, result.Page);
            Assert.AreEqual(10, result.PageSize);
        }

        [TestMethod]
        public void Search_NullFilter_IsTreatedAsNoFilter()
        {
            _service.Search(null, 1, 10);

            Assert.IsNull(_repository.LastFilter.Username);
            Assert.IsNull(_repository.LastFilter.Action);
            Assert.IsNull(_repository.LastFilter.FromUtc);
            Assert.IsNull(_repository.LastFilter.ToUtc);
        }

        [TestMethod]
        public void Search_BlankTextFilters_BecomeNull_AndTextIsTrimmed()
        {
            _service.Search(new AuditLogFilter { Username = "   ", Action = "  LoginSuccess " }, 1, 10);

            Assert.IsNull(_repository.LastFilter.Username);
            Assert.AreEqual("LoginSuccess", _repository.LastFilter.Action);
        }

        [TestMethod]
        public void Search_DoesNotChangeTheCallersFilter()
        {
            var filter = new AuditLogFilter { Action = "  LoginSuccess " };

            _service.Search(filter, 1, 10);

            Assert.AreEqual("  LoginSuccess ", filter.Action);
        }

        [TestMethod]
        public void Search_PageSizeBelowOne_UsesDefault()
        {
            _service.Search(new AuditLogFilter(), 1, 0);
            Assert.AreEqual(AuditLogQueryService.DefaultPageSize, _repository.LastPageSize);

            _service.Search(new AuditLogFilter(), 1, -5);
            Assert.AreEqual(AuditLogQueryService.DefaultPageSize, _repository.LastPageSize);
        }

        [TestMethod]
        public void Search_PageSizeAboveMax_IsCappedAtMax()
        {
            _service.Search(new AuditLogFilter(), 1, 5000);

            Assert.AreEqual(AuditLogQueryService.MaxPageSize, _repository.LastPageSize);
        }

        [TestMethod]
        public void Search_PageBelowOne_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => _service.Search(new AuditLogFilter(), 0, 10));
            Assert.AreEqual(0, _repository.Calls);
        }

        [TestMethod]
        public void Search_StartAfterEnd_Throws()
        {
            var filter = new AuditLogFilter { FromUtc = new DateTime(2026, 10, 5), ToUtc = new DateTime(2026, 10, 1) };

            Assert.ThrowsExactly<ArgumentException>(() => _service.Search(filter, 1, 10));
            Assert.AreEqual(0, _repository.Calls);
        }

        [TestMethod]
        public void Search_SingleDayRange_IsAllowed()
        {
            // "5 Oct to 5 Oct" reaches the service as 5 Oct 00:00 up to 6 Oct 00:00
            var filter = new AuditLogFilter { FromUtc = new DateTime(2026, 10, 5), ToUtc = new DateTime(2026, 10, 6) };

            _service.Search(filter, 1, 10);

            Assert.AreEqual(1, _repository.Calls);
        }

        [TestMethod]
        public void Constructor_NullRepository_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new AuditLogQueryService(null));
        }

        [TestMethod]
        public void QueryRepository_IsReadOnly_NoWriteMethods()
        {
            var forbiddenPrefixes = new[] { "Insert", "Update", "Delete", "Remove", "Add", "Edit", "Modify", "Clear" };

            var offending = typeof(IAuditLogQueryRepository)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)
                .Where(name => forbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Assert.AreEqual(0, offending.Count, "Write method found: " + string.Join(", ", offending));
        }
    }
}