using System;
using System.Data;
using System.Data.SqlClient;
using CourierService.Data;
using CourierService.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Integration
{
    /// <summary>
    /// What the connection factory does when the database can't be reached (T52). Uses real connections, so it needs
    /// LocalDB for two of the tests; without it they report Inconclusive. Nothing is written to any database.
    /// </summary>
    [TestClass]
    public class SqlConnectionFactoryIntegrationTests
    {
        private const string LocalDbMaster = @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;Connect Timeout=5;";

        [TestMethod]
        public void UnreachableServer_ThrowsDatabaseUnavailable()
        {
            // Port 1 on this machine: nothing listens there, so the connection is refused straight away
            var factory = new SqlConnectionFactory(
                "Server=tcp:127.0.0.1,1;Database=CourierService;Integrated Security=True;Connect Timeout=3;Pooling=False;");

            var ex = Assert.ThrowsExactly<DatabaseUnavailableException>(() => factory.CreateOpenConnection());

            Assert.IsInstanceOfType(ex.InnerException, typeof(SqlException));
            Assert.IsTrue(DatabaseOutage.IsOutage(ex));
        }

        [TestMethod]
        public void MissingDatabase_ThrowsDatabaseUnavailable()
        {
            RequireLocalDb();

            var factory = new SqlConnectionFactory(
                @"Server=(localdb)\MSSQLLocalDB;Database=CourierService_T52_DoesNotExist;Trusted_Connection=True;Connect Timeout=5;Pooling=False;");

            var ex = Assert.ThrowsExactly<DatabaseUnavailableException>(() => factory.CreateOpenConnection());

            Assert.IsTrue(DatabaseOutage.IsOutage(ex), "Error 4060 (cannot open database) counts as an outage");
        }

        [TestMethod]
        public void ReachableDatabase_StillReturnsAnOpenConnection()
        {
            RequireLocalDb();

            using (var connection = new SqlConnectionFactory(LocalDbMaster).CreateOpenConnection())
            {
                Assert.AreEqual(ConnectionState.Open, connection.State);
            }
        }

        private static void RequireLocalDb()
        {
            try
            {
                using (var connection = new SqlConnection(LocalDbMaster))
                {
                    connection.Open();
                }
            }
            catch (Exception ex)
            {
                Assert.Inconclusive("LocalDB is not available: " + ex.Message);
            }
        }
    }
}