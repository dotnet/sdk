// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using PublishCommand = Microsoft.DotNet.Tools.Publish.PublishCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetPublishInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetPublishInvocation));
        private readonly ITestOutputHelper output;

        public GivenDotnetPublishInvocation(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "-verbosity:m", "-tlp:default=auto", "-nologo"];
        private static readonly string[] ExpectedProperties = ["--property:_IsPublishing=true", "-property:NuGetInteractive=true"];

        [Theory]
        [InlineData(new string[] { }, new string[] { })]
        [InlineData(new string[] { "-r", "<rid>" }, new string[] { "-property:RuntimeIdentifier=<rid>", "-property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "-r", "linux-amd64" }, new string[] { "-property:RuntimeIdentifier=linux-x64", "-property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "--runtime", "<rid>" }, new string[] { "-property:RuntimeIdentifier=<rid>", "-property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "--use-current-runtime" }, new string[] { "-property:UseCurrentRuntimeIdentifier=True" })]
        [InlineData(new string[] { "--ucr" }, new string[] { "-property:UseCurrentRuntimeIdentifier=True" })]
        [InlineData(new string[] { "-o", "<publishdir>" }, new string[] { "-property:PublishDir=<cwd><publishdir>", "-property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--output", "<publishdir>" }, new string[] { "-property:PublishDir=<cwd><publishdir>", "-property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--artifacts-path", "foo" }, new string[] { "-property:ArtifactsPath=<cwd>foo" })]
        [InlineData(new string[] { "-c", "<config>" }, new string[] { "-property:Configuration=<config>", "-property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [InlineData(new string[] { "--configuration", "<config>" }, new string[] { "-property:Configuration=<config>", "-property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [InlineData(new string[] { "--version-suffix", "<versionsuffix>" }, new string[] { "-property:VersionSuffix=<versionsuffix>" })]
        [InlineData(new string[] { "--manifest", "<manifestfiles>" }, new string[] { "-property:TargetManifestFiles=<cwd><manifestfiles>" })]
        [InlineData(new string[] { "-v", "minimal" }, new string[] { "-verbosity:minimal" })]
        [InlineData(new string[] { "--verbosity", "minimal" }, new string[] { "-verbosity:minimal" })]
        [InlineData(new string[] { "<project>" }, new string[] { "<project>" })]
        [InlineData(new string[] { "<project>", "<extra-args>" }, new string[] { "<project>", "<extra-args>" })]
        [InlineData(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = PublishCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand
                    .Should()
                    .BeNull();

                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, "-restore", "-target:Publish", .. ExpectedProperties, .. expectedAdditionalArgs]);
            });
        }

        [Theory]
        [InlineData(new string[] { "-f", "<tfm>" }, new string[] { "-property:TargetFramework=<tfm>" })]
        [InlineData(new string[] { "--framework", "<tfm>" }, new string[] { "-property:TargetFramework=<tfm>" })]
        public void MsbuildInvocationIsCorrectForSeparateRestore(string[] args, string[] expectedAdditionalArgs)
        {
            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(args, msbuildPath);

            command.SeparateRestoreCommand
                   .GetArgumentTokensToMSBuild()
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "-target:Restore", "-tlp:verbosity=quiet", .. ExpectedProperties]);

            command.GetArgumentTokensToMSBuild()
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "-nologo", "-target:Publish", .. ExpectedProperties, .. expectedAdditionalArgs]);
        }

        [Fact]
        public void MsbuildInvocationIsCorrectForNoBuild()
        {
            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(new[] { "--no-build" }, msbuildPath);

            command.SeparateRestoreCommand
                   .Should()
                   .BeNull();

            command.GetArgumentTokensToMSBuild()
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "-target:Publish", .. ExpectedProperties, "-property:NoBuild=true"]);
        }

        [Fact]
        public void CommandAcceptsMultipleCustomProperties()
        {
            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(new[] { "/p:Prop1=prop1", "/p:Prop2=prop2" }, msbuildPath);

            command.GetArgumentTokensToMSBuild()
               .Should()
               .BeEquivalentTo([.. ExpectedPrefix, "-restore", "-target:Publish", .. ExpectedProperties, "--property:Prop1=prop1", "--property:Prop2=prop2"]);
        }
    }
}
