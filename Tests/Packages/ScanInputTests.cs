using CourierService.Domain.Entities;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Packages
{
    [TestClass]
    public class PackageIdentifierTests
    {
        [TestMethod]
        [DataRow("F20-0001")]
        [DataRow("f20-0001")]
        [DataRow("TEST-ab12cd34ef56")]
        [DataRow("PKG_42")]
        [DataRow("A")]
        [DataRow("123456789012345678901234567890")] // exactly 30
        public void WellFormedIdentifiers_AreAccepted_AsTheyAre(string scanned)
        {
            string identifier;

            Assert.IsTrue(PackageIdentifier.TryNormalize(scanned, out identifier));
            Assert.AreEqual(scanned, identifier);
        }

        [TestMethod]
        [DataRow("  F20-0001  ", "F20-0001")]
        [DataRow("F20-0001\r\n", "F20-0001")] // what a HID scanner types after the code
        [DataRow("\tF20-0001\n", "F20-0001")]
        public void ScannerNoise_AroundTheCode_IsTrimmed(string scanned, string expected)
        {
            string identifier;

            Assert.IsTrue(PackageIdentifier.TryNormalize(scanned, out identifier));
            Assert.AreEqual(expected, identifier);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("1234567890123456789012345678901")] // 31: longer than the column
        [DataRow("F20 0001")]                         // space inside
        [DataRow("F20-0001\nF20-0002")]               // two codes run together
        [DataRow("F20-0001'; DROP TABLE Packages;--")]
        [DataRow("F20-0001' OR '1'='1")]
        [DataRow("../../etc/passwd")]
        [DataRow("<script>alert(1)</script>")]
        [DataRow("F20–0001")]                    // en dash instead of a hyphen
        [DataRow("F20-0001%00")]
        public void MalformedInput_IsRejected(string scanned)
        {
            string identifier;

            Assert.IsFalse(PackageIdentifier.TryNormalize(scanned, out identifier));
            Assert.IsNull(identifier);
        }
    }

    [TestClass]
    public class PackageStatusParserTests
    {
        [TestMethod]
        [DataRow("Registered", PackageStatus.Registered)]
        [DataRow("InStorage", PackageStatus.InStorage)]
        [DataRow("ReadyForCollection", PackageStatus.ReadyForCollection)]
        [DataRow("Collected", PackageStatus.Collected)]
        [DataRow("instorage", PackageStatus.InStorage)]
        [DataRow("READYFORCOLLECTION", PackageStatus.ReadyForCollection)]
        [DataRow("In Storage", PackageStatus.InStorage)]              // the name staff see on screen
        [DataRow("Ready for Collection", PackageStatus.ReadyForCollection)]
        [DataRow("  collected  ", PackageStatus.Collected)]
        public void KnownNames_AreAccepted(string text, PackageStatus expected)
        {
            PackageStatus status;

            Assert.IsTrue(PackageStatusParser.TryParse(text, out status));
            Assert.AreEqual(expected, status);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("Banana")]
        [DataRow("Out for Delivery")] // not a status in this system (DECISIONS.md #7)
        [DataRow("Received")]
        [DataRow("Delivered")]
        [DataRow("Registered;")]
        [DataRow("Collected, InStorage")]
        public void UnknownNames_AreRejected(string text)
        {
            PackageStatus status;

            Assert.IsFalse(PackageStatusParser.TryParse(text, out status));
        }

        [TestMethod]
        [DataRow("0")]
        [DataRow("1")]
        [DataRow("3")]
        [DataRow("-1")]
        [DataRow("99")]
        public void Numbers_AreRejected_EvenThoughEnumTryParseWouldAcceptThem(string text)
        {
            PackageStatus status;

            Assert.IsFalse(PackageStatusParser.TryParse(text, out status));
        }
    }

    [TestClass]
    public class PersonalDataTests
    {
        [TestMethod]
        [DataRow("0821234567", "*******567")]
        [DataRow("+27821234567", "*********567")]
        [DataRow("  0821234567  ", "*******567")]
        public void MaskPhone_KeepsOnlyTheLastThreeCharacters(string phone, string expected)
        {
            Assert.AreEqual(expected, PersonalData.MaskPhone(phone));
        }

        [TestMethod]
        [DataRow("123", "***")]
        [DataRow("12", "**")]
        public void MaskPhone_ShortNumbers_AreFullyHidden(string phone, string expected)
        {
            Assert.AreEqual(expected, PersonalData.MaskPhone(phone));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void MaskPhone_BlankNumber_GivesNull(string phone)
        {
            Assert.IsNull(PersonalData.MaskPhone(phone));
        }

        [TestMethod]
        public void MaskPhone_NeverRevealsMoreThanThreeCharacters()
        {
            var masked = PersonalData.MaskPhone("0821234567");

            Assert.AreEqual(10, masked.Length);
            Assert.AreEqual(3, masked.Replace("*", string.Empty).Length);
        }

        [TestMethod]
        [DataRow("thandi@courier.test", "t*****@courier.test")]
        [DataRow("john@example.com", "j***@example.com")]
        [DataRow("a@example.test", "a@example.test")]
        [DataRow("  thandi@courier.test  ", "t*****@courier.test")]
        public void MaskEmail_MasksLocalPartAndPreservesDomain(string email, string expected)
        {
            Assert.AreEqual(expected, PersonalData.MaskEmail(email));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void MaskEmail_BlankAddress_ReturnsNull(string email)
        {
            Assert.IsNull(PersonalData.MaskEmail(email));
        }

        [TestMethod]
        [DataRow("invalid-address")]
        [DataRow("@example.com")]
        [DataRow("user@@example.com")]
        [DataRow("user@")]
        public void MaskEmail_InvalidAddress_IsFullyMasked(string email)
        {
            Assert.AreEqual(new string('*', email.Trim().Length), PersonalData.MaskEmail(email));
        }
    }
}
