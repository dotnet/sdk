// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Telemetry;
using RestoreCommand = Microsoft.DotNet.Cli.Commands.Restore.RestoreCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenDotnetRestoreInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo", "--target:Restore"];
        private static readonly string NuGetDisabledProperty = "--property:NuGetInteractive=false";
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetRestoreInvocation));

        [TestMethod]
        [DataRow(new string[] { }, new string[] { })]
        [DataRow(new string[] { "-s", "<source>" }, new string[] { "--property:RestoreSources=<source>" })]
        [DataRow(new string[] { "--source", "<source>" }, new string[] { "--property:RestoreSources=<source>" })]
        [DataRow(new string[] { "-s", "<source0>", "-s", "<source1>" }, new string[] { "--property:RestoreSources=<source0>%3B<source1>" })]
        [DataRow(new string[] { "-r", "<runtime>" }, new string[] { "--property:RuntimeIdentifiers=<runtime>" })]
        [DataRow(new string[] { "-r", "linux-amd64" }, new string[] { "--property:RuntimeIdentifiers=linux-x64" })]
        [DataRow(new string[] { "--runtime", "<runtime>" }, new string[] { "--property:RuntimeIdentifiers=<runtime>" })]
        [DataRow(new string[] { "-r", "<runtime0>", "-r", "<runtime1>" }, new string[] { "--property:RuntimeIdentifiers=<runtime0>%3B<runtime1>" })]
        [DataRow(new string[] { "--packages", "<packages>" }, new string[] { "--property:RestorePackagesPath=<cwd><packages>" })]
        [DataRow(new string[] { "--disable-parallel" }, new string[] { "--property:RestoreDisableParallel=true" })]
        [DataRow(new string[] { "--configfile", "<config>" }, new string[] { "--property:RestoreConfigFile=<cwd><config>" })]
        [DataRow(new string[] { "--no-cache" }, new string[] { "--property:RestoreNoCache=true" })]
        [DataRow(new string[] { "--no-http-cache" }, new string[] { "--property:RestoreNoHttpCache=true" })]
        [DataRow(new string[] { "--ignore-failed-sources" }, new string[] { "--property:RestoreIgnoreFailedSources=true" })]
        [DataRow(new string[] { "--no-dependencies" }, new string[] { "--property:RestoreRecursive=false" })]
        [DataRow(new string[] { "-v", "minimal" }, new string[] { "--verbosity:minimal" })]
        [DataRow(new string[] { "--verbosity", "minimal" }, new string[] { "--verbosity:minimal" })]
        [DataRow(new string[] { "--use-lock-file" }, new string[] { "--property:RestorePackagesWithLockFile=true" })]
        [DataRow(new string[] { "--locked-mode" }, new string[] { "--property:RestoreLockedMode=true" })]
        [DataRow(new string[] { "--force-evaluate" }, new string[] { "--property:RestoreForceEvaluate=true" })]
        [DataRow(new string[] { "--lock-file-path", "<lockFilePath>" }, new string[] { "--property:NuGetLockFilePath=<lockFilePath>" })]
        [DataRow(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                TelemetryClient.DisabledForTests = true;

                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                List<string> expectedArgs = [.. ExpectedPrefix, .. expectedAdditionalArgs, NuGetDisabledProperty];
                expectedArgs.Should().BeSubsetOf(
                ((MSBuildForwardingApp)RestoreCommand.FromArgs(args, msbuildPath))
                    .GetArgumentTokensToMSBuild()
                    );
            });
        }
    }
}

