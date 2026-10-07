using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Security
{
    /// <summary>
    /// The HTTPS port lives in two committed files: Web.csproj (IISExpressSSLPort, where Visual Studio serves HTTPS)
    /// and Web.config (Https.Port, where the redirect sends plain HTTP requests). If they drift apart, every HTTP
    /// request is redirected to a port nothing is listening on. Visual Studio can change the csproj value on its own,
    /// so this catches it before it is merged (T11).
    /// </summary>
    [TestClass]
    public class HttpsPortSettingsTests
    {
        private static readonly XNamespace MsBuild = "http://schemas.microsoft.com/developer/msbuild/2003";

        [TestMethod]
        public void ProjectSslPort_MatchesTheRedirectPortInWebConfig()
        {
            var root = FindRepositoryRoot();

            var project = XDocument.Load(Path.Combine(root, "Web", "CourierService.Web.csproj"));
            var sslPort = project.Descendants(MsBuild + "IISExpressSSLPort").Select(e => e.Value.Trim()).FirstOrDefault();

            var webConfig = XDocument.Load(Path.Combine(root, "Web", "Web.config"));
            var redirectPort = webConfig.Descendants("appSettings").Elements("add")
                .Where(e => (string)e.Attribute("key") == "Https.Port")
                .Select(e => ((string)e.Attribute("value") ?? "").Trim())
                .FirstOrDefault();

            Assert.IsFalse(string.IsNullOrEmpty(sslPort), "IISExpressSSLPort is missing from Web/CourierService.Web.csproj.");
            Assert.IsFalse(string.IsNullOrEmpty(redirectPort), "Https.Port is missing from Web/Web.config.");
            Assert.AreEqual(redirectPort, sslPort,
                "IISExpressSSLPort in Web.csproj must match Https.Port in Web.config (both 44301, see README). " +
                "Visual Studio sometimes changes the csproj port by itself; set it back before committing.");
        }

        // Walks up from the test output folder (Tests/bin/Debug/net48) to the folder with CourierService.sln
        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CourierService.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                Assert.Inconclusive("Could not find CourierService.sln above " + AppDomain.CurrentDomain.BaseDirectory);
            }

            return dir.FullName;
        }
    }
}