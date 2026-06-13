// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenMsbuildForwardingApp : SdkTest
    {
        public GivenMsbuildForwardingApp(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void DotnetExeIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet.exe");
        }

        [UnixOnlyFact]
        public void DotnetIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet");
        }

        [Theory]
        [InlineData("MSBuildExtensionsPath")]
        [InlineData("MSBuildSDKsPath")]
        [InlineData("DOTNET_CLI_TELEMETRY_SESSIONID")]
        public void ItSetsEnvironmentalVariables(string envVarName)
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath).GetProcessStartInfo();
            startInfo.Environment.ContainsKey(envVarName).Should().BeTrue();
        }

        [Fact]
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

        [Fact]
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

        [Fact]
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
                (sessionId == null || Guid.TryParse(sessionId, out _))
                    .Should().BeTrue("DOTNET_CLI_TELEMETRY_SESSIONID should be null or current session id");
            }
        }

        [Fact]
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

        [Fact]
        public void ItDoesNotSetCurrentWorkingDirectory()
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(Array.Empty<string>(), msbuildPath)
                .GetProcessStartInfo().WorkingDirectory.Should().Be("");
        }
    }
}
