// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RestoreCommand = Microsoft.DotNet.Tools.Restore.RestoreCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetRestoreInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "-verbosity:m", "-tlp:default=auto", "-nologo", "-target:Restore", "-property:NuGetInteractive=true"];
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetRestoreInvocation));

        [Theory]
        [InlineData(new string[] { }, new string[] { })]
        [InlineData(new string[] { "-s", "<source>" }, new string[] { "-property:RestoreSources=<source>" })]
        [InlineData(new string[] { "--source", "<source>" }, new string[] { "-property:RestoreSources=<source>" })]
        [InlineData(new string[] { "-s", "<source0>", "-s", "<source1>" }, new string[] { "-property:RestoreSources=<source0>%3B<source1>" })]
        [InlineData(new string[] { "-r", "<runtime>" }, new string[] { "-property:RuntimeIdentifiers=<runtime>" })]
        [InlineData(new string[] { "-r", "linux-amd64" }, new string[] { "-property:RuntimeIdentifiers=linux-x64" })]
        [InlineData(new string[] { "--runtime", "<runtime>" }, new string[] { "-property:RuntimeIdentifiers=<runtime>" })]
        [InlineData(new string[] { "-r", "<runtime0>", "-r", "<runtime1>" }, new string[] { "-property:RuntimeIdentifiers=<runtime0>%3B<runtime1>" })]
        [InlineData(new string[] { "--packages", "<packages>" }, new string[] { "-property:RestorePackagesPath=<cwd><packages>" })]
        [InlineData(new string[] { "--disable-parallel" }, new string[] { "-property:RestoreDisableParallel=true" })]
        [InlineData(new string[] { "--configfile", "<config>" }, new string[] { "-property:RestoreConfigFile=<cwd><config>" })]
        [InlineData(new string[] { "--no-cache" }, new string[] { "-property:RestoreNoCache=true" })]
        [InlineData(new string[] { "--no-http-cache" }, new string[] { "-property:RestoreNoHttpCache=true" })]
        [InlineData(new string[] { "--ignore-failed-sources" }, new string[] { "-property:RestoreIgnoreFailedSources=true" })]
        [InlineData(new string[] { "--no-dependencies" }, new string[] { "-property:RestoreRecursive=false" })]
        [InlineData(new string[] { "-v", "minimal" }, new string[] { "-verbosity:minimal" })]
        [InlineData(new string[] { "--verbosity", "minimal" }, new string[] { "-verbosity:minimal" })]
        [InlineData(new string[] { "--use-lock-file" }, new string[] { "-property:RestorePackagesWithLockFile=true" })]
        [InlineData(new string[] { "--locked-mode" }, new string[] { "-property:RestoreLockedMode=true" })]
        [InlineData(new string[] { "--force-evaluate" }, new string[] { "-property:RestoreForceEvaluate=true" })]
        [InlineData(new string[] { "--lock-file-path", "<lockFilePath>" }, new string[] { "-property:NuGetLockFilePath=<lockFilePath>" })]
        [InlineData(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                Telemetry.Telemetry.DisableForTests();

                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                RestoreCommand.FromArgs(args, msbuildPath)
                    .GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. expectedAdditionalArgs]);
            });
        }
    }
}
