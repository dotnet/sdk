// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenMsbuildForwardingApp : SdkTest
    {
        public GivenMsbuildForwardingApp()
        {
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void DotnetExeIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet.exe");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void DotnetIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet");
        }

        [TestMethod]
        [DataRow("MSBuildExtensionsPath")]
        [DataRow("MSBuildSDKsPath")]
        [DataRow("DOTNET_CLI_TELEMETRY_SESSIONID")]
        public void ItSetsEnvironmentalVariables(string envVarName)
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath).GetProcessStartInfo();
            startInfo.Environment.ContainsKey(envVarName).Should().BeTrue();
        }

        [TestMethod]
        public void ItPropagatesTheCurrentActivityContext()
        {
            using var activity = new Activity("invocation")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            activity.TraceStateString = "vendor=value";

            var startInfo = new MSBuildForwardingApp(Array.Empty<string>(), "<msbuildpath>").GetProcessStartInfo();

            startInfo.Environment[Activities.TRACEPARENT]
                .Should().Be($"00-{activity.TraceId}-{activity.SpanId}-{(byte)activity.Context.TraceFlags:x2}");
            startInfo.Environment[Activities.TRACESTATE].Should().Be(activity.TraceStateString);
        }

        [TestMethod]
        public void ItSetsMSBuildExtensionPathToExistingPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "MSBuildExtensionsPath";
            new DirectoryInfo(new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                                .GetProcessStartInfo()
                                .Environment[envVar])
                .Should()
                .Exist();
        }

        [TestMethod]
        public void ItSetsMSBuildSDKsPathToExistingPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "MSBuildSDKsPath";
            new DirectoryInfo(new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                                .GetProcessStartInfo()
                                .Environment[envVar])
                .Should()
                .Exist();
        }

        [TestMethod]
        public void ItSetsOrIgnoresTelemetrySessionId()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "DOTNET_CLI_TELEMETRY_SESSIONID";
            var startInfo = new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                .GetProcessStartInfo();

            string sessionId = startInfo.Environment[envVar];

            Log.WriteLine("StartInfo DOTNET_CLI_TELEMETRY_SESSIONID: " + sessionId);

            //  Other in-process tests (GivenADotnetFirstTimeUseConfigurerWithStateSetup) use "test"
            //  for session ID, so ignore if they already set it
            if (sessionId != "test")
            {
                (sessionId == null || Guid.TryParse(sessionId, out _) || sessionId == TelemetryClient.CurrentSessionId)
                    .Should().BeTrue("DOTNET_CLI_TELEMETRY_SESSIONID should be null, current session id, or a guid");
            }
        }

        [TestMethod]
        // TelemetryClient static state is process-wide and is accessed by code that cannot participate in a resource lock.
        [DoNotParallelize]
        public void ItUsesSeededTelemetrySessionId()
        {
            const string sessionId = "gha-12345-1";
            var msbuildPath = "<msbuildpath>";
            var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            TelemetryClient.DisabledForTests = true;
            TelemetryClient.DisabledForTests = false;

            try
            {
                environmentProvider
                    .Setup(p => p.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, It.IsAny<bool>()))
                    .Returns(false);
                environmentProvider
                    .Setup(p => p.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_SESSIONID))
                    .Returns(sessionId);

                _ = new TelemetryClient(sessionId: null, environmentProvider: environmentProvider.Object);

                var startInfo = new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                    .GetProcessStartInfo();

                startInfo.Environment["DOTNET_CLI_TELEMETRY_SESSIONID"].Should().Be(sessionId);
            }
            finally
            {
                TelemetryClient.DisabledForTests = true;
            }
        }

        [TestMethod]
        public void ItDoesNotSetCurrentWorkingDirectory()
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                .GetProcessStartInfo().WorkingDirectory.Should().Be("");
        }

        [TestMethod]
        public void ItEnablesMSBuildServerByDefault()
        {
            //  The SDK enables the MSBuild server by default. Only assert this when the ambient environment
            //  hasn't already expressed an opinion via MSBUILDUSESERVER or DOTNET_CLI_USE_MSBUILD_SERVER.
            if (Environment.GetEnvironmentVariable("MSBUILDUSESERVER") != null ||
                Environment.GetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER") != null)
            {
                return;
            }

            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath).GetProcessStartInfo();
            startInfo.Environment["MSBUILDUSESERVER"].Should().Be("1");
        }

        [TestMethod]
        [ResourceLock(WellKnownResources.EnvironmentVariables)]
        [DataRow(null, "0")]
        [DataRow("", "0")]
        [DataRow("0", "0")]
        [DataRow("1", "1")]
        public void ItUsesCliServerSettingUnlessMSBuildServerSettingIsExplicit(string msbuildUseServer, string expected)
        {
            string originalUseMSBuildServer = Environment.GetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER");
            string originalMSBuildUseServer = Environment.GetEnvironmentVariable("MSBUILDUSESERVER");

            try
            {
                Environment.SetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", "false");
                Environment.SetEnvironmentVariable("MSBUILDUSESERVER", msbuildUseServer);

                var msbuildPath = "<msbuildpath>";
                var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath).GetProcessStartInfo();
                startInfo.Environment["MSBUILDUSESERVER"].Should().Be(expected);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", originalUseMSBuildServer);
                Environment.SetEnvironmentVariable("MSBUILDUSESERVER", originalMSBuildUseServer);
            }
        }
    }
}
