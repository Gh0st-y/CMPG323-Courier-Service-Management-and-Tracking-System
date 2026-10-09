using CourierService.Services.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Security
{
    [TestClass]
    public class PageAccessRuleTests
    {
        private const string BrowserAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        private const string FetchAccept = "*/*";

        [TestMethod]
        public void LoggedIn_IsAllowedEverywhere()
        {
            Assert.AreEqual(PageAccessDecision.Allow, PageAccessRule.Evaluate(true, false, "/", BrowserAccept));
            Assert.AreEqual(PageAccessDecision.Allow, PageAccessRule.Evaluate(true, false, "/Packages/Search", BrowserAccept));
        }

        [TestMethod]
        public void NotLoggedIn_OpeningAPage_GoesToLogin()
        {
            Assert.AreEqual(PageAccessDecision.RedirectToLogin, PageAccessRule.Evaluate(false, false, "/", BrowserAccept));
            Assert.AreEqual(PageAccessDecision.RedirectToLogin, PageAccessRule.Evaluate(false, false, "/Packages/Detail/F20-0001", BrowserAccept));
        }

        [TestMethod]
        public void NotLoggedIn_ScriptAskingForData_Gets401()
        {
            Assert.AreEqual(PageAccessDecision.NotAuthenticated, PageAccessRule.Evaluate(false, false, "/Mock/Fixtures", FetchAccept));
            Assert.AreEqual(PageAccessDecision.NotAuthenticated, PageAccessRule.Evaluate(false, false, "/Mock/Fixtures", null));
        }

        [TestMethod]
        public void LoginPage_IsOpenToEveryone()
        {
            Assert.AreEqual(PageAccessDecision.Allow, PageAccessRule.Evaluate(false, true, "/Account/Login", BrowserAccept));
        }

        [TestMethod]
        public void ApiEndpoints_AreLeftToTheirOwnChecks()
        {
            Assert.AreEqual(PageAccessDecision.Allow, PageAccessRule.Evaluate(false, false, "/api/auth/login", FetchAccept));
            Assert.AreEqual(PageAccessDecision.Allow, PageAccessRule.Evaluate(false, false, "/API/packages", BrowserAccept));
            Assert.AreEqual(PageAccessDecision.Allow, PageAccessRule.Evaluate(false, false, "/api", FetchAccept));
        }

        [TestMethod]
        public void PathsThatOnlyStartWithApi_AreNotApi()
        {
            Assert.IsFalse(PageAccessRule.IsApiPath("/apiary"));
            Assert.IsFalse(PageAccessRule.IsApiPath("/Packages/api"));
            Assert.IsFalse(PageAccessRule.IsApiPath(null));
        }

        [TestMethod]
        public void LoginUrl_KeepsThePageToComeBackTo()
        {
            Assert.AreEqual("/Account/Login?returnUrl=%2FPackages%2FSearch%3Fstatus%3DInStorage",
                PageAccessRule.LoginUrl("/Packages/Search?status=InStorage", false));
        }

        [TestMethod]
        public void LoginUrl_SaysWhenTheSessionExpired()
        {
            Assert.AreEqual("/Account/Login?expired=1&returnUrl=%2FScan", PageAccessRule.LoginUrl("/Scan", true));
            Assert.AreEqual("/Account/Login?expired=1", PageAccessRule.LoginUrl("/", true));
        }

        [TestMethod]
        public void LoginUrl_DropsReturnAddressesOutsideTheSite()
        {
            Assert.AreEqual("/Account/Login", PageAccessRule.LoginUrl("//evil.example/steal", false));
            Assert.AreEqual("/Account/Login", PageAccessRule.LoginUrl("/\\evil.example", false));
            Assert.AreEqual("/Account/Login", PageAccessRule.LoginUrl("https://evil.example/", false));
            Assert.AreEqual("/Account/Login", PageAccessRule.LoginUrl(null, false));
            Assert.AreEqual("/Account/Login", PageAccessRule.LoginUrl("/", false));
        }
    }
}