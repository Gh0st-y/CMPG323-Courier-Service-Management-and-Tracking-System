using CourierService.Services.Users;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Users
{
    [TestClass]
    public class PasswordPolicyTests
    {
        private static bool Check(string password)
        {
            string message;
            return PasswordPolicy.IsValid(password, out message);
        }

        [TestMethod]
        public void EightCharactersWithLettersAndNumbers_IsValid()
        {
            Assert.IsTrue(Check("abcdefg1"));
            Assert.IsTrue(Check("Demo@2026!"));
        }

        [TestMethod]
        public void SevenCharacters_IsRejected()
        {
            Assert.IsFalse(Check("abcdef1"));
        }

        [TestMethod]
        public void NoNumber_IsRejected()
        {
            Assert.IsFalse(Check("abcdefgh"));
        }

        [TestMethod]
        public void NoLetter_IsRejected()
        {
            Assert.IsFalse(Check("12345678"));
            Assert.IsFalse(Check("1234!@#$"));
        }

        [TestMethod]
        public void NullOrEmpty_IsRejected()
        {
            Assert.IsFalse(Check(null));
            Assert.IsFalse(Check(""));
        }

        [TestMethod]
        public void SeventyTwoBytes_IsValid_SeventyThree_IsRejected()
        {
            // BCrypt ignores everything after 72 bytes, so longer passwords are refused rather than cut
            Assert.IsTrue(Check("a1" + new string('x', 70)));
            Assert.IsFalse(Check("a1" + new string('x', 71)));
        }

        [TestMethod]
        public void LengthLimitCountsBytesNotCharacters()
        {
            // "é" is 2 bytes in UTF-8, so 40 of them (80 bytes) is over the limit even though it is only 41 characters
            Assert.IsFalse(Check(new string('é', 40) + "1"));
        }

        [TestMethod]
        public void RejectionExplainsTheRule()
        {
            string message;
            PasswordPolicy.IsValid("short1", out message);
            Assert.AreEqual(PasswordPolicy.Requirement, message);
        }
    }
}