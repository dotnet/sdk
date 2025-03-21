// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using TestCommand = Microsoft.DotNet.Tools.Test.TestCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetTestInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "-verbosity:m", "-tlp:default=auto", "-nologo", "-restore", "-nologo", "-target:VSTest", "-property:NuGetInteractive=true"];

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetTestInvocation));

        [Theory]
        [InlineData(new string[] { "--disable-build-servers" },
            new string[] {
                "--property:UseRazorBuildServer=false",
                "--property:UseSharedCompilation=false",
                "/nodeReuse:false",
                "-property:VSTestArtifactsProcessingMode=collect",
                "-property:VSTestSessionCorrelationId=<testSessionCorrelationId>"
            })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                Telemetry.Telemetry.DisableForTests();

                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var testSessionCorrelationId = "<testSessionCorrelationId>";
                var msbuildPath = "<msbuildpath>";

                TestCommand.FromArgs(args, testSessionCorrelationId, msbuildPath)
                    .GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. expectedAdditionalArgs]);
            });
        }
    }
}
