// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Build;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetBuildInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        const string ExpectedPrefix = "-maxcpucount -verbosity:m";

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetBuildInvocation));

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-o", "foo" }, "-property:OutputPath=<cwd>foo -property:_CommandLineDefinedOutputPath=true")]
        [InlineData(new string[] { "-property:Verbosity=diag" }, "--property:Verbosity=diag")]
        [InlineData(new string[] { "--output", "foo" }, "-property:OutputPath=<cwd>foo -property:_CommandLineDefinedOutputPath=true")]
        [InlineData(new string[] { "--artifacts-path", "foo" }, "-property:ArtifactsPath=<cwd>foo")]
        [InlineData(new string[] { "-o", "foo1 foo2" }, "\"-property:OutputPath=<cwd>foo1 foo2\" -property:_CommandLineDefinedOutputPath=true")]
        [InlineData(new string[] { "--no-incremental" }, "-target:Rebuild")]
        [InlineData(new string[] { "-r", "rid" }, "-property:RuntimeIdentifier=rid -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "-r", "linux-amd64" }, "-property:RuntimeIdentifier=linux-x64 -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--runtime", "rid" }, "-property:RuntimeIdentifier=rid -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--use-current-runtime" }, "-property:UseCurrentRuntimeIdentifier=True")]
        [InlineData(new string[] { "--ucr" }, "-property:UseCurrentRuntimeIdentifier=True")]
        [InlineData(new string[] { "-c", "config" }, "-property:Configuration=config")]
        [InlineData(new string[] { "--configuration", "config" }, "-property:Configuration=config")]
        [InlineData(new string[] { "--version-suffix", "mysuffix" }, "-property:VersionSuffix=mysuffix")]
        [InlineData(new string[] { "--no-dependencies" }, "-property:BuildProjectReferences=false")]
        [InlineData(new string[] { "-v", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "--verbosity", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "--no-incremental", "-o", "myoutput", "-r", "myruntime", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
                                  "-target:Rebuild -property:RuntimeIdentifier=myruntime -property:_CommandLineDefinedRuntimeIdentifier=true -verbosity:diag -property:OutputPath=<cwd>myoutput -property:_CommandLineDefinedOutputPath=true /ArbitrarySwitchForMSBuild")]
        [InlineData(new string[] { "/t:CustomTarget" }, "/t:CustomTarget")]
        [InlineData(new string[] { "--disable-build-servers" }, "-p:UseRazorBuildServer=false -p:UseSharedCompilation=false /nodeReuse:false")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand.Should().BeNull();

                command.GetArgumentsToMSBuild()
                    .Should()
                    .Be($"{ExpectedPrefix} -restore -consoleloggerparameters:Summary{expectedAdditionalArgs}");
            });
        }

        [Theory]
        [InlineData(new string[] { "-f", "tfm" }, "-target:Restore", "-property:TargetFramework=tfm")]
        [InlineData(new string[] { "-p:TargetFramework=tfm" }, "-target:Restore", "--property:TargetFramework=tfm")]
        [InlineData(new string[] { "/p:TargetFramework=tfm" }, "-target:Restore", "--property:TargetFramework=tfm")]
        [InlineData(new string[] { "-t:Run", "-f", "tfm" }, "-target:Restore", "-property:TargetFramework=tfm -t:Run")]
        [InlineData(new string[] { "/t:Run", "-f", "tfm" }, "-target:Restore", "-property:TargetFramework=tfm /t:Run")]
        [InlineData(new string[] { "-o", "myoutput", "-f", "tfm", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
                                  "-target:Restore -verbosity:diag -property:OutputPath=<cwd>myoutput -property:_CommandLineDefinedOutputPath=true /ArbitrarySwitchForMSBuild",
                                  "-property:TargetFramework=tfm -verbosity:diag -property:OutputPath=<cwd>myoutput -property:_CommandLineDefinedOutputPath=true /ArbitrarySwitchForMSBuild")]
        public void MsbuildInvocationIsCorrectForSeparateRestore(
            string[] args,
            string expectedAdditionalArgsForRestore,
            string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgsForRestore = expectedAdditionalArgsForRestore.Replace("<cwd>", WorkingDirectory);

                expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");
                expectedAdditionalArgs = expectedAdditionalArgs.Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand.GetArgumentsToMSBuild()
                    .Should()
                    .Be($"{ExpectedPrefix} {expectedAdditionalArgsForRestore}");

                command.GetArgumentsToMSBuild()
                    .Should()
                    .Be($"{ExpectedPrefix} -nologo -consoleloggerparameters:Summary{expectedAdditionalArgs}");
            });
        }
    }
}
