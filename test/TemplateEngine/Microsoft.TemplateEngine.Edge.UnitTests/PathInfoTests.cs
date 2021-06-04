// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class PathInfoTests
    {
        [Fact]
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

            Assert.Equal(homeFolder, pathInfo.UserProfileDir);
            Assert.Equal(Path.Combine(homeFolder, ".templateengine"), pathInfo.GlobalSettingsDir);
            Assert.Equal(Path.Combine(homeFolder, ".templateengine", "hostID"), pathInfo.HostSettingsDir);
            Assert.Equal(Path.Combine(homeFolder, ".templateengine", "hostID", "1.0.0"), pathInfo.HostVersionSettingsDir);
        }

        [Fact]
        public void DefaultLocationTest_ExpectedExceptions()
        {
            var environment = A.Fake<IEnvironment>();
            A.CallTo(() => environment.GetEnvironmentVariable("HOME")).Returns("/home/path");
            A.CallTo(() => environment.GetEnvironmentVariable("USERPROFILE")).Returns("C:\\users\\user");

            var host = A.Fake<ITemplateEngineHost>();
            A.CallTo(() => host.HostIdentifier).Returns("hostID");
            A.CallTo(() => host.Version).Returns("");
            Assert.Throws<ArgumentException>(() => new DefaultPathInfo(environment, host));

            A.CallTo(() => host.HostIdentifier).Returns("");
            A.CallTo(() => host.Version).Returns("ver");
            Assert.Throws<ArgumentException>(() => new DefaultPathInfo(environment, host));
        }

        [Theory]
        [InlineData ("global", "host", "version")]
        [InlineData (null, "host", "version")]
        [InlineData ("global", null, "version")]
        [InlineData ("global", "host", null)]
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

            Assert.Equal(homeFolder, pathInfo.UserProfileDir);
            Assert.Equal(string.IsNullOrWhiteSpace(global) ? defaultGlobal : global, pathInfo.GlobalSettingsDir);
            Assert.Equal(string.IsNullOrWhiteSpace(hostDir) ? defaultHost : hostDir, pathInfo.HostSettingsDir);
            Assert.Equal(string.IsNullOrWhiteSpace(hostVersion) ? defaultHostVesrion : hostVersion, pathInfo.HostVersionSettingsDir);
        }

        [Theory]
        [InlineData("custom")]
        [InlineData(null)]
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

            Assert.Equal(homeFolder, pathInfo.UserProfileDir);
            Assert.Equal(expectedGlobal, pathInfo.GlobalSettingsDir);
            Assert.Equal(expectedHost, pathInfo.HostSettingsDir);
            Assert.Equal(expectedHostVesrion, pathInfo.HostVersionSettingsDir);
        }
    }
}
