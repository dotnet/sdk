// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using PublishCommand = Microsoft.DotNet.Cli.Commands.Publish.PublishCommand;

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

        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo"];
        private static readonly string[] ExpectedProperties = ["--property:_IsPublishing=true"];
        private static readonly string NuGetDisabledProperty = "--property:NuGetInteractive=false";

        [Theory]
        [InlineData(new string[] { }, new string[] { })]
        [InlineData(new string[] { "-r", "<rid>" }, new string[] { "--property:RuntimeIdentifier=<rid>", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "-r", "linux-amd64" }, new string[] { "--property:RuntimeIdentifier=linux-x64", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "--runtime", "<rid>" }, new string[] { "--property:RuntimeIdentifier=<rid>", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [InlineData(new string[] { "--use-current-runtime" }, new string[] { "--property:UseCurrentRuntimeIdentifier=True" })]
        [InlineData(new string[] { "--ucr" }, new string[] { "--property:UseCurrentRuntimeIdentifier=True" })]
        [InlineData(new string[] { "-o", "<publishdir>" }, new string[] { "--property:PublishDir=<cwd><publishdir>", "--property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--output", "<publishdir>" }, new string[] { "--property:PublishDir=<cwd><publishdir>", "--property:_CommandLineDefinedOutputPath=true" })]
        [InlineData(new string[] { "--artifacts-path", "foo" }, new string[] { "--property:ArtifactsPath=<cwd>foo" })]
        [InlineData(new string[] { "-c", "<config>" }, new string[] { "--property:Configuration=<config>", "--property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [InlineData(new string[] { "--configuration", "<config>" }, new string[] { "--property:Configuration=<config>", "--property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [InlineData(new string[] { "--version-suffix", "<versionsuffix>" }, new string[] { "--property:VersionSuffix=<versionsuffix>" })]
        [InlineData(new string[] { "--manifest", "<manifestfiles>" }, new string[] { "--property:TargetManifestFiles=<cwd><manifestfiles>" })]
        [InlineData(new string[] { "-v", "minimal" }, new string[] { "--verbosity:minimal" })]
        [InlineData(new string[] { "--verbosity", "minimal" }, new string[] { "--verbosity:minimal" })]
        [InlineData(new string[] { "<project>" }, new string[] { "<project>" })]
        [InlineData(new string[] { "<project>", "<extra-args>" }, new string[] { "<project>", "<extra-args>" })]
        [InlineData(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory).Replace("<publishdir>", "<publishdir>" + Path.DirectorySeparatorChar))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = (PublishCommand)PublishCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand
                    .Should()
                    .BeNull();

                List<string> expected = [.. ExpectedPrefix, "-restore", "--target:Publish", .. ExpectedProperties, .. expectedAdditionalArgs, NuGetDisabledProperty, .. GivenDotnetBuildInvocation.RestoreExpectedPrefixForImplicitRestore];
                expected.Should().BeSubsetOf(command.GetArgumentTokensToMSBuild());
            });
        }

        [Theory]
        [InlineData(new string[] { "-f", "<tfm>" }, new string[] { "--property:TargetFramework=<tfm>" })]
        [InlineData(new string[] { "--framework", "<tfm>" }, new string[] { "--property:TargetFramework=<tfm>" })]
        public void MsbuildInvocationIsCorrectForSeparateRestore(string[] args, string[] expectedAdditionalArgs)
        {
            var msbuildPath = "<msbuildpath>";
            var command = (PublishCommand)PublishCommand.FromArgs(args, msbuildPath);

            var restoreTokens =
                command.SeparateRestoreCommand! // for this scenario, we expect a separate restore command
                   .GetArgumentTokensToMSBuild();
            output.WriteLine("restore tokens:");
            output.WriteLine(string.Join(" ", restoreTokens));
            restoreTokens
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "--target:Restore", "-tlp:verbosity=quiet", .. ExpectedProperties, NuGetDisabledProperty, .. GivenDotnetBuildInvocation.RestoreExpectedPrefixForSeparateRestore]);

            var buildTokens =
                command.GetArgumentTokensToMSBuild();
            output.WriteLine("build tokens:");
            output.WriteLine(string.Join(" ", buildTokens));

            buildTokens
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "--target:Publish", .. ExpectedProperties, .. expectedAdditionalArgs, NuGetDisabledProperty]);
        }

        [Fact]
        public void MsbuildInvocationIsCorrectForNoBuild()
        {
            var msbuildPath = "<msbuildpath>";
            var command = (PublishCommand)PublishCommand.FromArgs(new[] { "--no-build" }, msbuildPath);

            command.SeparateRestoreCommand
                   .Should()
                   .BeNull();

            command.GetArgumentTokensToMSBuild()
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "--target:Publish", .. ExpectedProperties, "--property:NoBuild=true", NuGetDisabledProperty]);
        }

        [Fact]
        public void CommandAcceptsMultipleCustomProperties()
        {
            var msbuildPath = "<msbuildpath>";
            var command = (PublishCommand)PublishCommand.FromArgs(new[] { "/p:Prop1=prop1", "/p:Prop2=prop2" }, msbuildPath);

            command.GetArgumentTokensToMSBuild()
               .Should()
               .Contain(["--property:Prop1=prop1", "--property:Prop2=prop2"]);
        }

        [Fact]
        public void OutputPathWithSemicolonIsEscaped()
        {
            // Test that semicolons in the output path are properly escaped for MSBuild
            var workingDirectory = TestPathUtilities.FormatAbsolutePath("t;e;s;t");
            CommandDirectoryContext.PerformActionWithBasePath(workingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = (PublishCommand)PublishCommand.FromArgs(new[] { "-o", "dist" }, msbuildPath);
                
                var tokens = command.GetArgumentTokensToMSBuild();
                
                // Find the PublishDir property token
                var publishDirToken = tokens.FirstOrDefault(t => t.StartsWith("--property:PublishDir="));
                publishDirToken.Should().NotBeNull("PublishDir property should be set");
                
                // The path should contain escaped semicolons (%3B) instead of raw semicolons
                publishDirToken.Should().Contain("%3B", "semicolons should be escaped");
                publishDirToken.Should().NotContain(";", "raw semicolons should not be present after property name");
                publishDirToken.Should().EndWith(Path.DirectorySeparatorChar.ToString(), "path should end with directory separator");
            });
        }

        [Theory]
        [InlineData("path;with;semicolons", "%3B")]
        [InlineData("path%with%percent", "%25")]
        [InlineData("path$with$dollar", "%24")]
        [InlineData("path@with@at", "%40")]
        [InlineData("path'with'apostrophe", "%27")]
        [InlineData("path*with*asterisk", "%2A")]
        [InlineData("path?with?question", "%3F")]
        public void OutputPathWithSpecialCharactersIsEscaped(string pathPart, string expectedEscapeSequence)
        {
            // Test that MSBuild special characters in the output path are properly escaped
            var workingDirectory = TestPathUtilities.FormatAbsolutePath(pathPart);
            CommandDirectoryContext.PerformActionWithBasePath(workingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = (PublishCommand)PublishCommand.FromArgs(new[] { "-o", "dist" }, msbuildPath);
                
                var tokens = command.GetArgumentTokensToMSBuild();
                
                // Find the PublishDir property token
                var publishDirToken = tokens.FirstOrDefault(t => t.StartsWith("--property:PublishDir="));
                publishDirToken.Should().NotBeNull("PublishDir property should be set");
                
                // The path should contain the expected escape sequence
                publishDirToken.Should().Contain(expectedEscapeSequence, $"special characters should be escaped as {expectedEscapeSequence}");
            });
        }
    }
}
