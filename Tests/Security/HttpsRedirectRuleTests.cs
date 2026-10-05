using CourierService.Services.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Security
{
    [TestClass]
    public class HttpsRedirectRuleTests
    {
        [TestMethod]
        public void Localhost_KeepsPathAndQuery()
        {
            string url;

            var ok = HttpsRedirectRule.TryBuildHttpsUrl("localhost", 44301, "/Scan/Index?x=1", out url);

            Assert.IsTrue(ok);
            Assert.AreEqual("https://localhost:44301/Scan/Index?x=1", url);
        }

        [TestMethod]
        public void LanAddress_IsKept()
        {
            string url;

            var ok = HttpsRedirectRule.TryBuildHttpsUrl("192.168.0.11", 44301, "/", out url);

            Assert.IsTrue(ok);
            Assert.AreEqual("https://192.168.0.11:44301/", url);
        }

        [TestMethod]
        public void BadHost_IsNotRedirected()
        {
            string url;

            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl(null, 44301, "/", out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("", 44301, "/", out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("bad host", 44301, "/", out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("evil.com/x", 44301, "/", out url));
            Assert.IsNull(url);
        }

        [TestMethod]
        public void BadPort_IsNotRedirected()
        {
            string url;

            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", 0, "/", out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", -1, "/", out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", 70000, "/", out url));
        }

        [TestMethod]
        public void BadPath_IsNotRedirected()
        {
            string url;

            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", 44301, null, out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", 44301, "", out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", 44301, "evil", out url));
        }

        [TestMethod]
        public void PathWithLineBreak_IsNotRedirected()
        {
            string url;

            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", 44301, "/a\r\nSet-Cookie: x=1", out url));
            Assert.IsFalse(HttpsRedirectRule.TryBuildHttpsUrl("localhost", 44301, "/a\nb", out url));
        }
    }
}