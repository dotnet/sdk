// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Restore;
using BuildCommand = Microsoft.DotNet.Cli.Commands.Build.BuildCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetBuildInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        string[] ExpectedPrefix = ["-maxcpucount", "-verbosity:m", "-tlp:default=auto", "-nologo"];

        const string NugetInteractiveProperty = "-property:NuGetInteractive=false";

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetBuildInvocation));

        [Theory]
        [InlineData(new string[] { }, new string[] { })]
        [InlineData(new string[] { "-o", "foo" }, new string[] { "-property:OutputPath=<cwd>foo", "-property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "-property:Verbosity=diag" }, new string[] { "--property:Verbosity=diag" })]
        [InlineData(new string[] { "--output", "foo" }, new string[] { "-property:OutputPath=<cwd>foo", "-property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--artifacts-path", "foo" }, new string[] { "-property:ArtifactsPath=<cwd>foo" })]
        [InlineData(new string[] { "-o", "foo1 foo2" }, new string[] { "-property:OutputPath=<cwd>foo1 foo2", "-property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--no-incremental" }, new string[] { "-target:Rebuild" })]
        [InlineData(new string[] { "-r", "rid" }, new string[] { "-property:RuntimeIdentifier=rid", "-property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "-r", "linux-amd64" }, new string[] { "-property:RuntimeIdentifier=linux-x64", "-property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "--runtime", "rid" }, new string[] { "-property:RuntimeIdentifier=rid", "-property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "--use-current-runtime" }, new string[] { "-property:UseCurrentRuntimeIdentifier=True" })]
        [InlineData(new string[] { "--ucr" }, new string[] { "-property:UseCurrentRuntimeIdentifier=True" })]
        [InlineData(new string[] { "-c", "config" }, new string[] { "-property:Configuration=config" })]
        [InlineData(new string[] { "--configuration", "config" }, new string[] { "-property:Configuration=config" })]
        [InlineData(new string[] { "--version-suffix", "mysuffix" }, new string[] { "-property:VersionSuffix=mysuffix" })]
        [InlineData(new string[] { "--no-dependencies" }, new string[] { "-property:BuildProjectReferences=false" })]
        [InlineData(new string[] { "-v", "diag" }, new string[] { "-verbosity:diag" })]
        [InlineData(new string[] { "--verbosity", "diag" }, new string[] { "-verbosity:diag" })]
        [InlineData(new string[] { "--no-incremental", "-o", "myoutput", "-r", "myruntime", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
                   new string[] { "-target:Rebuild", "-property:RuntimeIdentifier=myruntime", "-property:_CommandLineDefinedRuntimeIdentifier=true", "-verbosity:diag", "-property:OutputPath=<cwd>myoutput", "-property:_CommandLineDefinedOutputPath=true", "/ArbitrarySwitchForMSBuild" })]
        [InlineData(new string[] { "/t:CustomTarget" }, new string[] { "/t:CustomTarget" })]
        [InlineData(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs.Select(arg => arg.Replace("<cwd>", WorkingDirectory)).ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = (RestoringCommand)BuildCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand.Should().BeNull();
                var commandArgs = command.GetArgumentTokensToMSBuild();
                commandArgs[0..6].Should().BeEquivalentTo([.. ExpectedPrefix, "-restore", "-consoleloggerparameters:Summary"]);
                commandArgs[6..].Should()
                    .BeEquivalentTo([NugetInteractiveProperty, .. expectedAdditionalArgs]);
            });
        }

        [Theory]
        [InlineData(new string[] { "-f", "tfm" },
            new string[] { "-target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "-property:TargetFramework=tfm" })]
        [InlineData(new string[] { "-p:TargetFramework=tfm" },
            new string[] { "-target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "--property:TargetFramework=tfm" })]
        [InlineData(new string[] { "/p:TargetFramework=tfm" },
            new string[] { "-target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "--property:TargetFramework=tfm" })]
        [InlineData(new string[] { "-t:Run", "-f", "tfm" },
            new string[] { "-target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "-property:TargetFramework=tfm", "-t:Run" })]
        [InlineData(new string[] { "/t:Run", "-f", "tfm" },
            new string[] { "-target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "-property:TargetFramework=tfm", "/t:Run" })]
        [InlineData(new string[] { "-o", "myoutput", "-f", "tfm", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
            new string[] { "-target:Restore", "-tlp:verbosity=quiet", "-verbosity:diag", "-property:OutputPath=<cwd>myoutput", "-property:_CommandLineDefinedOutputPath=true", "/ArbitrarySwitchForMSBuild" },
            new string[] { "-property:TargetFramework=tfm", "-verbosity:diag", "-property:OutputPath=<cwd>myoutput", "-property:_CommandLineDefinedOutputPath=true", "/ArbitrarySwitchForMSBuild" })]
        [InlineData(new string[] { "-f", "tfm", "-getItem:Compile", "-getProperty:TargetFramework", "-getTargetResult:Build" },
            new string[] { "-target:Restore", "-tlp:verbosity=quiet", "-nologo", "-verbosity:quiet" },
            new string[] { "-property:TargetFramework=tfm", "-getItem:Compile", "-getProperty:TargetFramework", "-getTargetResult:Build" })]
        public void MsbuildInvocationIsCorrectForSeparateRestore(
            string[] args,
            string[] expectedAdditionalArgsForRestore,
            string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgsForRestore = expectedAdditionalArgsForRestore
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = (RestoringCommand)BuildCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. expectedAdditionalArgsForRestore, NugetInteractiveProperty]);

                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, "-nologo", "-consoleloggerparameters:Summary", NugetInteractiveProperty, .. expectedAdditionalArgs]);
            });
        }
    }
}
