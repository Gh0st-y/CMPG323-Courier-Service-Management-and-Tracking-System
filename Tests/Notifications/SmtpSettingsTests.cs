using System.Collections.Specialized;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    [TestClass]
    public class SmtpSettingsTests
    {
        [TestMethod]
        public void ReadsTheWebConfigKeys()
        {
            var settings = SmtpSettings.FromAppSettings(new NameValueCollection
            {
                { "Smtp.Host", " mail.f20.local " },
                { "Smtp.Port", "587" },
                { "Smtp.FromAddress", "courier-noreply@f20.local" },
                { "Smtp.FromName", "F20 Courier" },
                { "Smtp.UseSsl", "True" },
                { "Smtp.UserName", "relay" },
                { "Smtp.Password", "secret" },
                { "Smtp.TimeoutSeconds", "20" }
            });

            Assert.AreEqual("mail.f20.local", settings.Host);
            Assert.AreEqual(587, settings.Port);
            Assert.AreEqual("courier-noreply@f20.local", settings.FromAddress);
            Assert.AreEqual("F20 Courier", settings.FromName);
            Assert.IsTrue(settings.UseSsl);
            Assert.AreEqual("relay", settings.UserName);
            Assert.AreEqual("secret", settings.Password);
            Assert.AreEqual(20, settings.TimeoutSeconds);
            Assert.IsNull(settings.Problem());
        }

        [TestMethod]
        public void MissingOrBadOptionalValues_UseTheDefaults()
        {
            var settings = SmtpSettings.FromAppSettings(new NameValueCollection
            {
                { "Smtp.Host", "localhost" },
                { "Smtp.Port", "not a number" },
                { "Smtp.FromAddress", "courier-noreply@f20.local" },
                { "Smtp.TimeoutSeconds", "0" }
            });

            Assert.AreEqual(25, settings.Port);
            Assert.AreEqual(10, settings.TimeoutSeconds);
            Assert.AreEqual("F20 Courier Service", settings.FromName);
            Assert.IsFalse(settings.UseSsl);
            Assert.IsNull(settings.UserName);
        }

        [TestMethod]
        public void Problem_NamesTheMissingOrBadSetting()
        {
            Assert.AreEqual("Smtp.Host is not set.",
                SmtpSettings.FromAppSettings(new NameValueCollection { { "Smtp.FromAddress", "a@f20.local" } }).Problem());

            Assert.AreEqual("Smtp.FromAddress is not set.",
                SmtpSettings.FromAppSettings(new NameValueCollection { { "Smtp.Host", "localhost" } }).Problem());

            Assert.AreEqual("Smtp.FromAddress is not a valid email address.",
                SmtpSettings.FromAppSettings(new NameValueCollection { { "Smtp.Host", "localhost" }, { "Smtp.FromAddress", "not an address" } }).Problem());
        }
    }
}