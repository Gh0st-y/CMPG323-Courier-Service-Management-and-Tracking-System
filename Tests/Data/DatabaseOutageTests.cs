using System;
using CourierService.Data;
using CourierService.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Data
{
    [TestClass]
    public class DatabaseOutageTests
    {
        [TestMethod]
        public void ErrorsThatMeanTheDatabaseIsDown_AreOutages()
        {
            Assert.IsTrue(DatabaseOutage.IsOutageNumber(942), "database offline");
            Assert.IsTrue(DatabaseOutage.IsOutageNumber(4060), "cannot open database");
            Assert.IsTrue(DatabaseOutage.IsOutageNumber(-2), "timeout");
            Assert.IsTrue(DatabaseOutage.IsOutageNumber(53), "server not found");
            Assert.IsTrue(DatabaseOutage.IsOutageNumber(10054), "connection reset");
        }

        [TestMethod]
        public void ErrorsThatMeanABugOrBadData_AreNotOutages()
        {
            // These must stay 500s (or be handled), not be hidden behind "try again later"
            Assert.IsFalse(DatabaseOutage.IsOutageNumber(547), "foreign key violation");
            Assert.IsFalse(DatabaseOutage.IsOutageNumber(2627), "unique constraint violation");
            Assert.IsFalse(DatabaseOutage.IsOutageNumber(208), "invalid object name");
            Assert.IsFalse(DatabaseOutage.IsOutageNumber(8152), "string would be truncated");
        }

        [TestMethod]
        public void DatabaseUnavailableException_IsAnOutage()
        {
            Assert.IsTrue(DatabaseOutage.IsOutage(new DatabaseUnavailableException(new Exception("connect failed"))));
        }

        [TestMethod]
        public void DatabaseUnavailableException_WrappedInsideAnotherException_IsAnOutage()
        {
            var wrapped = new InvalidOperationException("outer", new DatabaseUnavailableException(new Exception("connect failed")));

            Assert.IsTrue(DatabaseOutage.IsOutage(wrapped));
        }

        [TestMethod]
        public void OrdinaryExceptions_AreNotOutages()
        {
            Assert.IsFalse(DatabaseOutage.IsOutage(new InvalidOperationException("a bug")));
            Assert.IsFalse(DatabaseOutage.IsOutage(new ArgumentException("bad", new NullReferenceException())));
            Assert.IsFalse(DatabaseOutage.IsOutage(null));
        }
    }
}