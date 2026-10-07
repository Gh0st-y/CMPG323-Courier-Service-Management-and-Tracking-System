using System;
using System.Linq;
using CourierService.Domain.Entities;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Packages
{
    [TestClass]
    public class PackageStatusTransitionsTests
    {
        private const PackageStatus Registered = PackageStatus.Registered;
        private const PackageStatus InStorage = PackageStatus.InStorage;
        private const PackageStatus Ready = PackageStatus.ReadyForCollection;
        private const PackageStatus Collected = PackageStatus.Collected;

        // Every (from, to) pair, written out one by one. The lifecycle is a straight line
        // (DECISIONS.md #7): Registered -> In Storage -> Ready for Collection -> Collected.
        // If the rules ever change, this table has to change on purpose.

        // ---- the 3 allowed moves ----
        [TestMethod]
        [DataRow(Registered, InStorage, true)]
        [DataRow(InStorage, Ready, true)]
        [DataRow(Ready, Collected, true)]

        // ---- no staying put ----
        [DataRow(Registered, Registered, false)]
        [DataRow(InStorage, InStorage, false)]
        [DataRow(Ready, Ready, false)]
        [DataRow(Collected, Collected, false)]

        // ---- no skipping ahead ----
        [DataRow(Registered, Ready, false)]
        [DataRow(Registered, Collected, false)]
        [DataRow(InStorage, Collected, false)]

        // ---- no going back ----
        [DataRow(InStorage, Registered, false)]
        [DataRow(Ready, InStorage, false)]
        [DataRow(Ready, Registered, false)]

        // ---- Collected is the end ----
        [DataRow(Collected, Ready, false)]
        [DataRow(Collected, InStorage, false)]
        [DataRow(Collected, Registered, false)]
        public void IsAllowed_MatchesTheRulesForEveryPair(PackageStatus from, PackageStatus to, bool expected)
        {
            Assert.AreEqual(expected, PackageStatusTransitions.IsAllowed(from, to),
                from + " -> " + to + " should " + (expected ? "" : "not ") + "be allowed.");
        }

        [TestMethod]
        public void TheTableAboveCoversEveryPair()
        {
            // 4 statuses means 16 pairs. If a fifth status is ever added this fails, as a reminder to
            // add its rows to the table above rather than leave them untested.
            var statusCount = Enum.GetValues(typeof(PackageStatus)).Length;

            Assert.AreEqual(4, statusCount, "A status was added or removed: update IsAllowed_MatchesTheRulesForEveryPair.");
        }

        [TestMethod]
        public void ExactlyThreePairsAreAllowed()
        {
            var statuses = Enum.GetValues(typeof(PackageStatus)).Cast<PackageStatus>().ToList();

            var allowedPairs = (from f in statuses
                                from t in statuses
                                where PackageStatusTransitions.IsAllowed(f, t)
                                select f + "->" + t).ToList();

            CollectionAssert.AreEquivalent(
                new[] { "Registered->InStorage", "InStorage->ReadyForCollection", "ReadyForCollection->Collected" },
                allowedPairs);
        }

        [TestMethod]
        public void AllowedNext_ListsOnlyTheNextStep()
        {
            CollectionAssert.AreEqual(new[] { InStorage }, PackageStatusTransitions.AllowedNext(Registered).ToArray());
            CollectionAssert.AreEqual(new[] { Ready }, PackageStatusTransitions.AllowedNext(InStorage).ToArray());
            CollectionAssert.AreEqual(new[] { Collected }, PackageStatusTransitions.AllowedNext(Ready).ToArray());
        }

        [TestMethod]
        public void AllowedNext_IsEmptyForCollected()
        {
            Assert.AreEqual(0, PackageStatusTransitions.AllowedNext(Collected).Count);
        }

        [TestMethod]
        public void RejectionMessage_MatchesTheExampleInTheApiContract()
        {
            Assert.AreEqual(
                "Package cannot move to Collected from Registered.",
                PackageStatusTransitions.RejectionMessage(Registered, Collected));
        }

        [TestMethod]
        public void RejectionMessage_UsesTheNamesStaffSeeOnScreen()
        {
            Assert.AreEqual(
                "Package cannot move to Ready for Collection from Registered.",
                PackageStatusTransitions.RejectionMessage(Registered, Ready));
            Assert.AreEqual(
                "Package cannot move to In Storage from Ready for Collection.",
                PackageStatusTransitions.RejectionMessage(Ready, InStorage));
        }

        [TestMethod]
        public void RejectionMessage_ForNoChange_SaysItIsAlreadyThere()
        {
            Assert.AreEqual("Package is already In Storage.", PackageStatusTransitions.RejectionMessage(InStorage, InStorage));
        }

        [TestMethod]
        public void DisplayName_ExistsForEveryStatus_AndIsNeverBlank()
        {
            foreach (PackageStatus status in Enum.GetValues(typeof(PackageStatus)))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(PackageStatusTransitions.DisplayName(status)), status.ToString());
            }
        }
    }
}
