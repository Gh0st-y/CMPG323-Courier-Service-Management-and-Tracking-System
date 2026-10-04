using System.Linq;
using System.Reflection;
using CourierService.Services.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Security
{
    [TestClass]
    public class RoleAccessRuleTests
    {
        [TestMethod]
        public void NoRole_IsNotAuthenticated()
        {
            Assert.AreEqual(AccessDecision.NotAuthenticated,
                RoleAccessRule.Evaluate(null, RoleNames.Supervisor));
        }

        [TestMethod]
        public void BlankRole_IsNotAuthenticated()
        {
            Assert.AreEqual(AccessDecision.NotAuthenticated, RoleAccessRule.Evaluate("   ", RoleNames.Supervisor));
            Assert.AreEqual(AccessDecision.NotAuthenticated, RoleAccessRule.Evaluate(""));
        }

        [TestMethod]
        public void NoRole_IsNotAuthenticated_EvenWhenAnyLoggedInUserIsAllowed()
        {
            // "any authenticated" must still refuse people who are not logged in
            Assert.AreEqual(AccessDecision.NotAuthenticated, RoleAccessRule.Evaluate(null));
        }

        [TestMethod]
        public void AnyLoggedInUser_IsAllowed_WhenNoRolesListed()
        {
            Assert.AreEqual(AccessDecision.Allowed, RoleAccessRule.Evaluate(RoleNames.IntakeClerk));
            Assert.AreEqual(AccessDecision.Allowed, RoleAccessRule.Evaluate(RoleNames.CollectionStaff));
        }

        [TestMethod]
        public void RoleInList_IsAllowed()
        {
            Assert.AreEqual(AccessDecision.Allowed,
                RoleAccessRule.Evaluate(RoleNames.Supervisor, RoleNames.Supervisor, RoleNames.SystemAdmin));
        }

        [TestMethod]
        public void RoleNotInList_IsForbidden()
        {
            Assert.AreEqual(AccessDecision.Forbidden,
                RoleAccessRule.Evaluate(RoleNames.IntakeClerk, RoleNames.Supervisor, RoleNames.SystemAdmin));
        }

        [TestMethod]
        public void AuditLogRule_OnlySupervisorAndAdminPass()
        {
            var allowed = new[] { RoleNames.Supervisor, RoleNames.SystemAdmin };
            var all = new[]
            {
                RoleNames.IntakeClerk, RoleNames.StorageStaff, RoleNames.CollectionStaff,
                RoleNames.Supervisor, RoleNames.SystemAdmin
            };

            var passing = all.Where(r => RoleAccessRule.Evaluate(r, allowed) == AccessDecision.Allowed).ToArray();

            CollectionAssert.AreEquivalent(allowed, passing);
        }

        [TestMethod]
        public void StatusUpdateRule_MatchesContractRoles()
        {
            var allowed = new[] { RoleNames.StorageStaff, RoleNames.Supervisor, RoleNames.SystemAdmin };

            Assert.AreEqual(AccessDecision.Allowed, RoleAccessRule.Evaluate(RoleNames.StorageStaff, allowed));
            Assert.AreEqual(AccessDecision.Forbidden, RoleAccessRule.Evaluate(RoleNames.CollectionStaff, allowed));
            Assert.AreEqual(AccessDecision.Forbidden, RoleAccessRule.Evaluate(RoleNames.IntakeClerk, allowed));
        }

        [TestMethod]
        public void RoleMatch_IgnoresCase()
        {
            Assert.AreEqual(AccessDecision.Allowed, RoleAccessRule.Evaluate("systemadmin", RoleNames.SystemAdmin));
        }

        [TestMethod]
        public void UnknownRole_IsForbidden_WhenRolesAreListed()
        {
            Assert.AreEqual(AccessDecision.Forbidden, RoleAccessRule.Evaluate("Hacker", RoleNames.SystemAdmin));
        }

        [TestMethod]
        public void RoleNames_AreTheFiveRolesInTheContract()
        {
            var values = typeof(RoleNames)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (string)f.GetRawConstantValue())
                .ToArray();

            var expected = new[] { "IntakeClerk", "StorageStaff", "CollectionStaff", "Supervisor", "SystemAdmin" };

            CollectionAssert.AreEquivalent(expected, values);
        }
    }
}