// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry;
using TestCommand = Microsoft.DotNet.Cli.Commands.Test.TestCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenDotnetTestInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo", "-restore", "-target:VSTest", "--property:NuGetInteractive=false"];

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetTestInvocation));

        [TestMethod]
        [DataRow(new string[] { "--disable-build-servers" },
            new string[] {
                "--property:UseRazorBuildServer=false",
                "--property:UseSharedCompilation=false",
                "/nodeReuse:false",
                "--property:VSTestArtifactsProcessingMode=collect",
                "--property:VSTestSessionCorrelationId=<testSessionCorrelationId>"
            })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                TelemetryClient.DisabledForTests = true;

                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var testSessionCorrelationId = "<testSessionCorrelationId>";
                var msbuildPath = "<msbuildpath>";

                expectedAdditionalArgs.Should().BeSubsetOf(TestCommand.FromArgs(args, testSessionCorrelationId, msbuildPath).GetArgumentTokensToMSBuild());
            });
        }
    }
}

