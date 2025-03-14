// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanCommand = Microsoft.DotNet.Tools.Clean.CleanCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetCleanInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private const string NugetInteractiveProperty = "-property:NuGetInteractive=true";
        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "-verbosity:m", "-tlp:default=auto", "-nologo", "-verbosity:normal", "-target:Clean", NugetInteractiveProperty];


        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetCleanInvocation));

        [Fact]
        public void ItAddsProjectToMsbuildInvocation()
        {
            var msbuildPath = "<msbuildpath>";
            CleanCommand.FromArgs(new string[] { "<project>" }, msbuildPath)
                .GetArgumentTokensToMSBuild()
                .Should()
                .BeEquivalentTo([.. ExpectedPrefix, "<project>"]);
        }

        [Theory]
        [InlineData(new string[] { }, new string[] { })]
        [InlineData(new string[] { "-o", "<output>" },
            new string[] { "-property:OutputPath=<cwd><output>", "-property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--output", "<output>" },
            new string[] { "-property:OutputPath=<cwd><output>", "-property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--artifacts-path", "foo" },
            new string[] { "-property:ArtifactsPath=<cwd>foo" })]
        [InlineData(new string[] { "-f", "<framework>" },
            new string[] { "-property:TargetFramework=<framework>" })]
        [InlineData(new string[] { "--framework", "<framework>" },
            new string[] { "-property:TargetFramework=<framework>" })]
        [InlineData(new string[] { "-c", "<configuration>" },
            new string[] { "-property:Configuration=<configuration>" })]
        [InlineData(new string[] { "--configuration", "<configuration>" },
            new string[] { "-property:Configuration=<configuration>" })]
        [InlineData(new string[] { "-v", "diag" },
            new string[] { "-verbosity:diag" })]
        [InlineData(new string[] { "--verbosity", "diag" },
            new string[] { "-verbosity:diag" })]
        [InlineData(new string[] { "--disable-build-servers" },
            new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                CleanCommand.FromArgs(args, msbuildPath)
                    .GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. expectedAdditionalArgs]);
            });
        }
    }
}
