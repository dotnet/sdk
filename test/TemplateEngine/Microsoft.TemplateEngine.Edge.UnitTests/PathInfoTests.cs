// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class PathInfoTests
    {
        [TestMethod]
        public void DefaultLocationTest()
        {
            var environment = A.Fake<IEnvironment>();
            A.CallTo(() => environment.GetEnvironmentVariable("HOME")).Returns("/home/path");
            A.CallTo(() => environment.GetEnvironmentVariable("USERPROFILE")).Returns("C:\\users\\user");

            var host = A.Fake<ITemplateEngineHost>();
            A.CallTo(() => host.HostIdentifier).Returns("hostID");
            A.CallTo(() => host.Version).Returns("1.0.0");

            var pathInfo = new DefaultPathInfo(environment, host);

            var homeFolder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\users\\user" : "/home/path";

            Assert.AreEqual(homeFolder, pathInfo.UserProfileDir);
            Assert.AreEqual(Path.Combine(homeFolder, ".templateengine"), pathInfo.GlobalSettingsDir);
            Assert.AreEqual(Path.Combine(homeFolder, ".templateengine", "hostID"), pathInfo.HostSettingsDir);
            Assert.AreEqual(Path.Combine(homeFolder, ".templateengine", "hostID", "1.0.0"), pathInfo.HostVersionSettingsDir);
        }

        [TestMethod]
        public void DefaultLocationTest_ExpectedExceptions()
        {
            var environment = A.Fake<IEnvironment>();
            A.CallTo(() => environment.GetEnvironmentVariable("HOME")).Returns("/home/path");
            A.CallTo(() => environment.GetEnvironmentVariable("USERPROFILE")).Returns("C:\\users\\user");

            var host = A.Fake<ITemplateEngineHost>();
            A.CallTo(() => host.HostIdentifier).Returns("hostID");
            A.CallTo(() => host.Version).Returns(string.Empty);
            Assert.ThrowsExactly<ArgumentException>(() => new DefaultPathInfo(environment, host));

            A.CallTo(() => host.HostIdentifier).Returns(string.Empty);
            A.CallTo(() => host.Version).Returns("ver");
            Assert.ThrowsExactly<ArgumentException>(() => new DefaultPathInfo(environment, host));
        }

        [TestMethod]
        [DataRow("global", "host", "version")]
        [DataRow(null, "host", "version")]
        [DataRow("global", null, "version")]
        [DataRow("global", "host", null)]
        public void CustomLocationTest(string? global, string? hostDir, string? hostVersion)
        {
            var environment = A.Fake<IEnvironment>();
            A.CallTo(() => environment.GetEnvironmentVariable("HOME")).Returns("/home/path");
            A.CallTo(() => environment.GetEnvironmentVariable("USERPROFILE")).Returns("C:\\users\\user");

            var host = A.Fake<ITemplateEngineHost>();
            A.CallTo(() => host.HostIdentifier).Returns("hostID");
            A.CallTo(() => host.Version).Returns("1.0.0");

            var pathInfo = new DefaultPathInfo(environment, host, global, hostDir, hostVersion);

            var homeFolder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\users\\user" : "/home/path";
            var defaultGlobal = Path.Combine(homeFolder, ".templateengine");
            var defaultHost = Path.Combine(homeFolder, ".templateengine", "hostID");
            var defaultHostVesrion = Path.Combine(homeFolder, ".templateengine", "hostID", "1.0.0");

            Assert.AreEqual(homeFolder, pathInfo.UserProfileDir);
            Assert.AreEqual(string.IsNullOrWhiteSpace(global) ? defaultGlobal : global, pathInfo.GlobalSettingsDir);
            Assert.AreEqual(string.IsNullOrWhiteSpace(hostDir) ? defaultHost : hostDir, pathInfo.HostSettingsDir);
            Assert.AreEqual(string.IsNullOrWhiteSpace(hostVersion) ? defaultHostVesrion : hostVersion, pathInfo.HostVersionSettingsDir);
        }

        [TestMethod]
        [DataRow("custom")]
        [DataRow(null)]
        public void CustomHiveLocationTest(string? hiveLocation)
        {
            var environment = A.Fake<IEnvironment>();
            A.CallTo(() => environment.GetEnvironmentVariable("HOME")).Returns("/home/path");
            A.CallTo(() => environment.GetEnvironmentVariable("USERPROFILE")).Returns("C:\\users\\user");

            var host = A.Fake<ITemplateEngineHost>();
            A.CallTo(() => host.HostIdentifier).Returns("hostID");
            A.CallTo(() => host.Version).Returns("1.0.0");

            var envSettings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => envSettings.Host).Returns(host);
            A.CallTo(() => envSettings.Environment).Returns(environment);

            var pathInfo = new DefaultPathInfo(envSettings, hiveLocation);

            var homeFolder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\users\\user" : "/home/path";
            var expectedGlobal = string.IsNullOrWhiteSpace(hiveLocation)
                ? Path.Combine(homeFolder, ".templateengine")
                : Path.Combine(hiveLocation);
            var expectedHost = string.IsNullOrWhiteSpace(hiveLocation)
                ? Path.Combine(homeFolder, ".templateengine", "hostID")
                : Path.Combine(hiveLocation, "hostID");
            var expectedHostVesrion = string.IsNullOrWhiteSpace(hiveLocation)
                ? Path.Combine(homeFolder, ".templateengine", "hostID", "1.0.0")
                : Path.Combine(hiveLocation, "hostID", "1.0.0");

            Assert.AreEqual(homeFolder, pathInfo.UserProfileDir);
            Assert.AreEqual(expectedGlobal, pathInfo.GlobalSettingsDir);
            Assert.AreEqual(expectedHost, pathInfo.HostSettingsDir);
            Assert.AreEqual(expectedHostVesrion, pathInfo.HostVersionSettingsDir);
        }
    }
}
