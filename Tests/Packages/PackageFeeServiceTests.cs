using System;
using System.Collections.Generic;
using CourierService.Domain.Repositories;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CourierService.Tests.Packages
{
    [TestClass]
    public class PackageFeeServiceTests
    {
        private sealed class Config : IAppConfigRepository
        {
            public Dictionary<string, string> Values = new Dictionary<string, string>();
            public string GetValue(string key) => Values.ContainsKey(key) ? Values[key] : null;
        }
        [TestMethod]
        public void ReadsChangedFeesWithoutRecreatingTheService()
        {
            var config = new Config();
            config.Values["Fee.Personal"] = "10.00";
            config.Values["Fee.WorkRelated"] = "0.00";
            var service = new PackageFeeService(config);
            Assert.AreEqual(10m, service.GetFee("Personal"));
            Assert.AreEqual(0m, service.GetFee("WorkRelated"));
            config.Values["Fee.Personal"] = "12.50";
            Assert.AreEqual(12.50m, service.GetFee("Personal"));
        }
        [TestMethod]
        public void RejectsMissingNegativeOverPrecisionAndOverflowFees()
        {
            var config = new Config();
            var service = new PackageFeeService(config);
            foreach (var value in new[] { null, "", "bad", "-1", "10000.00", "1.001" })
            {
                config.Values["Fee.Personal"] = value;
                Assert.ThrowsExactly<InvalidOperationException>(() => service.GetFee("Personal"));
            }
        }
        [TestMethod]
        public void UsesInvariantCurrencyAndRejectsUnknownClassification()
        {
            var config = new Config();
            config.Values["Fee.Personal"] = "9999.99";
            var service = new PackageFeeService(config);
            Assert.AreEqual(9999.99m, service.GetFee("Personal"));
            Assert.ThrowsExactly<ArgumentException>(() => service.GetFee("Other"));
        }
    }
}
