// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.MSBuild;

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
            new MSBuildForwardingApp(new string[0], msbuildPath)
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet.exe");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void DotnetIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            new MSBuildForwardingApp(new string[0], msbuildPath)
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet");
        }

        [TestMethod]
        [DataRow("MSBuildExtensionsPath")]
        [DataRow("MSBuildSDKsPath")]
        [DataRow("DOTNET_CLI_TELEMETRY_SESSIONID")]
        public void ItSetsEnvironmentalVariables(string envVarName)
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath).GetProcessStartInfo();
            startInfo.Environment.ContainsKey(envVarName).Should().BeTrue();
        }

        [TestMethod]
        public void ItSetsMSBuildExtensionPathToExistingPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "MSBuildExtensionsPath";
            new DirectoryInfo(new MSBuildForwardingApp(new string[0], msbuildPath)
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
            new DirectoryInfo(new MSBuildForwardingApp(new string[0], msbuildPath)
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
            var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath)
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

        [TestMethod]
        public void ItDoesNotSetCurrentWorkingDirectory()
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = new MSBuildForwardingApp(new string[0], msbuildPath)
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
    }
}
