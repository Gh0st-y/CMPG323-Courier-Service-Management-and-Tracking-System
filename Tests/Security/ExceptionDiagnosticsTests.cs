using System;
using CourierService.Services.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Security
{
    [TestClass]
    public class ExceptionDiagnosticsTests
    {
        [TestMethod]
        public void Format_PreservesTypeAndFailureLocationWithoutSensitiveMessages()
        {
            try
            {
                throw new InvalidOperationException("recipient@example.test password=private", new Exception("SQL contains personal data"));
            }
            catch (Exception exception)
            {
                var diagnostic = ExceptionDiagnostics.Format(exception);
                Assert.IsTrue(diagnostic.Contains(typeof(InvalidOperationException).FullName));
                Assert.IsTrue(diagnostic.Contains(nameof(Format_PreservesTypeAndFailureLocationWithoutSensitiveMessages)));
                Assert.IsFalse(diagnostic.Contains("recipient@example.test"));
                Assert.IsFalse(diagnostic.Contains("password=private"));
                Assert.IsFalse(diagnostic.Contains("SQL contains personal data"));
            }
        }

        [TestMethod]
        public void Format_UnthrownExceptionStillIncludesItsTypeOnly()
        {
            Assert.AreEqual(typeof(Exception).FullName + Environment.NewLine, ExceptionDiagnostics.Format(new Exception("private")));
        }
    }
}
