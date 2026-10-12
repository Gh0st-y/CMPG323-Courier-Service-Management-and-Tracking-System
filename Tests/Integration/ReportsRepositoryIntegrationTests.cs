using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CourierService.Tests.Integration
{
    [TestClass]
    public class ReportsRepositoryIntegrationTests
    {
        private string _connectionString;
        private readonly List<int> _packages = new List<int>();
        private int _recipient, _user, _role;
        private ReportsRepository _repository;
        [TestInitialize]
        public void SetUp()
        {
            _connectionString = Environment.GetEnvironmentVariable("COURIER_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(_connectionString))
                Assert.Inconclusive("Set COURIER_TEST_CONNECTION_STRING to an isolated TaskVerification database.");
            Assert.IsTrue(new SqlConnectionStringBuilder(_connectionString).InitialCatalog.StartsWith("CourierService_TaskVerification_", StringComparison.Ordinal),
                "Only an isolated TaskVerification database is allowed.");
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                _role = (int)Scalar(connection, "INSERT dbo.Roles(RoleName) VALUES (@Value); SELECT CAST(SCOPE_IDENTITY() AS int);", "RPT-" + suffix);
                _user = (int)Scalar(connection, "INSERT dbo.Users(Username, Email, PasswordHash, RoleId) VALUES (@Value, @Value + '@courier.test', 'unused', " + _role + "); SELECT CAST(SCOPE_IDENTITY() AS int);", "RPT-" + suffix);
                _recipient = (int)Scalar(connection, "INSERT dbo.Recipients(FullName) VALUES (@Value); SELECT CAST(SCOPE_IDENTITY() AS int);", "Synthetic report " + suffix);
                Add(connection, suffix + "A", "2040-01-01T21:59:59", "Registered", "Personal", "Unpaid", 10m);
                Add(connection, suffix + "B", "2040-01-01T22:00:00", "Registered", "Personal", "Unpaid", 10m);
                Add(connection, suffix + "C", "2040-01-02T08:00:00", "ReadyForCollection", "WorkRelated", "Exempt", 0m);
                Add(connection, suffix + "D", "2040-01-02T21:59:59", "Collected", "Personal", "Paid", 12.5m, "2040-01-03T21:59:59");
                Add(connection, suffix + "E", "2040-01-02T22:00:00", "InStorage", "Personal", "Unpaid", 10m);
            }
            _repository = new ReportsRepository(new SqlConnectionFactory(_connectionString));
        }
        [TestCleanup]
        public void CleanUp()
        {
            if (_recipient == 0) return;
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                Scalar(connection, "DELETE dbo.Packages WHERE RecipientId = @Value; DELETE dbo.Recipients WHERE RecipientId = @Value;", _recipient);
                Scalar(connection, "DELETE dbo.Users WHERE UserId = @Value;", _user);
                Scalar(connection, "DELETE dbo.Roles WHERE RoleId = @Value;", _role);
            }
        }
        [TestMethod]
        public void TotalsAndOutstandingCollectionsRespectSouthAfricanDayBoundaries()
        {
            var report = _repository.GetReport(Day());
            Assert.AreEqual(3, report.Kpis.Total);
            Assert.AreEqual(1, report.Kpis.ReadyForCollection);
            Assert.AreEqual(1, report.Kpis.Collected);
            Assert.AreEqual(12.5m, report.Kpis.FeesPaid);
            Assert.AreEqual(10m, report.Kpis.FeesUnpaid);
            Assert.AreEqual(1d, report.Kpis.AverageDaysToCollect.Value);
            Assert.AreEqual(2, report.Outstanding.Count);
            Assert.IsTrue(report.Outstanding.All(p => p.Status != "Collected"));
            Assert.AreEqual("2040-01-02", report.PerDay.Single().Label);
            Assert.AreEqual(3m, report.PerDay.Single().Value);
        }
        [TestMethod]
        public void FiltersAndFeeGroupsAgreeWithTheSelectedPackages()
        {
            var filter = Day(); filter.PaymentStatus = "Paid"; filter.Classification = "Personal";
            var report = _repository.GetReport(filter);
            Assert.AreEqual(1, report.Kpis.Total);
            Assert.AreEqual(12.5m, report.ByPaymentStatus.Single().Value);
            Assert.AreEqual("Paid", report.ByPaymentStatus.Single().Label);
            Assert.AreEqual(0, report.Outstanding.Count);
            filter = Day(); filter.Status = "ReadyForCollection";
            Assert.AreEqual(1, _repository.GetReport(filter).Kpis.Total);
        }
        [TestMethod]
        public void DailyVolumesIncludeZeroDaysAndEmptyRangesHaveZeroFees()
        {
            var filter = Day(); filter.ToDate = new DateTime(2040, 1, 4);
            var report = _repository.GetReport(filter);
            CollectionAssert.AreEqual(new decimal[] { 3m, 1m, 0m }, report.PerDay.Select(p => p.Value).ToArray());
            report = _repository.GetReport(new ReportFilter { FromDate = new DateTime(2041, 1, 1), ToDate = new DateTime(2041, 1, 1) });
            Assert.AreEqual(0, report.Kpis.Total);
            Assert.AreEqual(0m, report.Kpis.FeesPaid);
            Assert.AreEqual(0m, report.Kpis.FeesUnpaid);
            Assert.IsNull(report.Kpis.AverageDaysToCollect);
            Assert.AreEqual(0, report.Outstanding.Count);
        }
        private static ReportFilter Day() => new ReportFilter { FromDate = new DateTime(2040, 1, 2), ToDate = new DateTime(2040, 1, 2) };
        private void Add(SqlConnection connection, string id, string received, string status, string classification, string payment, decimal fee, string collected = null)
        {
            using (var command = new SqlCommand(@"INSERT dbo.Packages(F20Identifier,RecipientId,CreatedByUserId,CreatedAtUtc,Status,Classification,PaymentStatus,Fee,CollectedAtUtc)
                VALUES(@Id,@Recipient,@User,@Received,@Status,@Class,@Payment,@Fee,@Collected); SELECT CAST(SCOPE_IDENTITY() AS int);", connection))
            {
                command.Parameters.AddWithValue("@Id", "RPT-" + id);
                command.Parameters.AddWithValue("@Recipient", _recipient); command.Parameters.AddWithValue("@User", _user);
                command.Parameters.AddWithValue("@Received", DateTime.Parse(received)); command.Parameters.AddWithValue("@Status", status);
                command.Parameters.AddWithValue("@Class", classification); command.Parameters.AddWithValue("@Payment", payment);
                command.Parameters.AddWithValue("@Fee", fee); command.Parameters.AddWithValue("@Collected", collected == null ? (object)DBNull.Value : DateTime.Parse(collected));
                _packages.Add((int)command.ExecuteScalar());
            }
        }
        private static object Scalar(SqlConnection connection, string sql, object value)
        {
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Value", value);
                return command.ExecuteScalar();
            }
        }
    }
}
