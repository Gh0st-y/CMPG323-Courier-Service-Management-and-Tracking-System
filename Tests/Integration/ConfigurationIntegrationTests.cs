using System;
using System.Data.SqlClient;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CourierService.Tests.Integration
{
    [TestClass]
    public class ConfigurationIntegrationTests
    {
        [TestMethod]
        public void FeeChangeInDatabaseIsReadWithoutRecreatingTheService()
        {
            var connectionString = Environment.GetEnvironmentVariable("COURIER_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
                Assert.Inconclusive("Set COURIER_TEST_CONNECTION_STRING to an isolated TaskVerification database.");
            var builder = new SqlConnectionStringBuilder(connectionString);
            Assert.IsTrue(builder.InitialCatalog.StartsWith("CourierService_TaskVerification_", StringComparison.Ordinal),
                "This test writes configuration; only an isolated TaskVerification database is allowed.");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string original;
                using (var read = new SqlCommand("SELECT ConfigValue FROM dbo.AppConfig WHERE ConfigKey = 'Fee.Personal';", connection))
                    original = (string)read.ExecuteScalar();
                var service = new PackageFeeService(new AppConfigRepository(new SqlConnectionFactory(connectionString)));
                try
                {
                    Update(connection, "12.50");
                    Assert.AreEqual(12.50m, service.GetFee("Personal"));
                    Update(connection, "15.00");
                    Assert.AreEqual(15m, service.GetFee("Personal"));
                }
                finally { Update(connection, original); }
            }
        }
        private static void Update(SqlConnection connection, string value)
        {
            using (var command = new SqlCommand("UPDATE dbo.AppConfig SET ConfigValue = @Value WHERE ConfigKey = 'Fee.Personal';", connection))
            {
                command.Parameters.AddWithValue("@Value", value);
                Assert.AreEqual(1, command.ExecuteNonQuery());
            }
        }
    }
}
