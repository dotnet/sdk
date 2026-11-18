// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using PublishCommand = Microsoft.DotNet.Cli.Commands.Publish.PublishCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenDotnetPublishInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetPublishInvocation));
        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo"];
        private static readonly string[] ExpectedProperties = ["--property:_IsPublishing=true"];
        private static readonly string NuGetDisabledProperty = "--property:NuGetInteractive=false";

        [TestMethod]
        [DataRow(new string[] { }, new string[] { })]
        [DataRow(new string[] { "-r", "<rid>" }, new string[] { "--property:RuntimeIdentifier=<rid>", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [DataRow(new string[] { "-r", "linux-amd64" }, new string[] { "--property:RuntimeIdentifier=linux-x64", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [DataRow(new string[] { "--runtime", "<rid>" }, new string[] { "--property:RuntimeIdentifier=<rid>", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [DataRow(new string[] { "--use-current-runtime" }, new string[] { "--property:UseCurrentRuntimeIdentifier=True" })]
        [DataRow(new string[] { "--ucr" }, new string[] { "--property:UseCurrentRuntimeIdentifier=True" })]
        [DataRow(new string[] { "-o", "<publishdir>" }, new string[] { "--property:PublishDir=<cwd><publishdir>", "--property:_CommandLineDefinedOutputPath=true" })]
        [DataRow(new string[] { "--output", "<publishdir>" }, new string[] { "--property:PublishDir=<cwd><publishdir>", "--property:_CommandLineDefinedOutputPath=true" })]
        [DataRow(new string[] { "--artifacts-path", "foo" }, new string[] { "--property:ArtifactsPath=<cwd>foo" })]
        [DataRow(new string[] { "-c", "<config>" }, new string[] { "--property:Configuration=<config>", "--property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [DataRow(new string[] { "--configuration", "<config>" }, new string[] { "--property:Configuration=<config>", "--property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [DataRow(new string[] { "--version-suffix", "<versionsuffix>" }, new string[] { "--property:VersionSuffix=<versionsuffix>" })]
        [DataRow(new string[] { "--manifest", "<manifestfiles>" }, new string[] { "--property:TargetManifestFiles=<cwd><manifestfiles>" })]
        [DataRow(new string[] { "-v", "minimal" }, new string[] { "--verbosity:minimal" })]
        [DataRow(new string[] { "--verbosity", "minimal" }, new string[] { "--verbosity:minimal" })]
        [DataRow(new string[] { "<project>" }, new string[] { "<project>" })]
        [DataRow(new string[] { "<project>", "<extra-args>" }, new string[] { "<project>", "<extra-args>" })]
        [DataRow(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory).Replace("<publishdir>", "<publishdir>" + Path.DirectorySeparatorChar))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = (PublishCommand)PublishCommand.FromArgs(args, msbuildPath, TestCommandServices.CreateNonLLM());

                command.SeparateRestoreCommand
                    .Should()
                    .BeNull();

                List<string> expected = [.. ExpectedPrefix, "-restore", "--target:Publish", .. ExpectedProperties, .. expectedAdditionalArgs, NuGetDisabledProperty, .. GivenDotnetBuildInvocation.RestoreExpectedPrefixForImplicitRestore];
                expected.Should().BeSubsetOf(command.GetArgumentTokensToMSBuild());
            });
        }

        [TestMethod]
        [DataRow(new string[] { "-f", "<tfm>" }, new string[] { "--property:TargetFramework=<tfm>" })]
        [DataRow(new string[] { "--framework", "<tfm>" }, new string[] { "--property:TargetFramework=<tfm>" })]
        public void MsbuildInvocationIsCorrectForSeparateRestore(string[] args, string[] expectedAdditionalArgs)
        {
            var msbuildPath = "<msbuildpath>";
            var command = (PublishCommand)PublishCommand.FromArgs(args, msbuildPath, TestCommandServices.CreateNonLLM());

            var restoreTokens =
                command.SeparateRestoreCommand! // for this scenario, we expect a separate restore command
                   .GetArgumentTokensToMSBuild();
            Log.WriteLine("restore tokens:");
            Log.WriteLine(string.Join(" ", restoreTokens));
            restoreTokens
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "--target:Restore", "-tlp:verbosity=quiet", .. ExpectedProperties, NuGetDisabledProperty, .. GivenDotnetBuildInvocation.RestoreExpectedPrefixForSeparateRestore]);

            var buildTokens =
                command.GetArgumentTokensToMSBuild();
            Log.WriteLine("build tokens:");
            Log.WriteLine(string.Join(" ", buildTokens));

            buildTokens
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "--target:Publish", .. ExpectedProperties, .. expectedAdditionalArgs, NuGetDisabledProperty]);
        }

        [TestMethod]
        public void MsbuildInvocationIsCorrectForNoBuild()
        {
            var msbuildPath = "<msbuildpath>";
            var command = (PublishCommand)PublishCommand.FromArgs(new[] { "--no-build" }, msbuildPath, TestCommandServices.CreateNonLLM());

            command.SeparateRestoreCommand
                   .Should()
                   .BeNull();

            command.GetArgumentTokensToMSBuild()
                   .Should()
                   .BeEquivalentTo([.. ExpectedPrefix, "--target:Publish", .. ExpectedProperties, "--property:NoBuild=true", NuGetDisabledProperty]);
        }

        [TestMethod]
        public void CommandAcceptsMultipleCustomProperties()
        {
            var msbuildPath = "<msbuildpath>";
            var command = (PublishCommand)PublishCommand.FromArgs(new[] { "/p:Prop1=prop1", "/p:Prop2=prop2" }, msbuildPath, TestCommandServices.CreateNonLLM());

            command.GetArgumentTokensToMSBuild()
               .Should()
               .Contain(["--property:Prop1=prop1", "--property:Prop2=prop2"]);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CustomMSBuildPathIsForwarded(bool separateRestore)
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory(identifier: separateRestore.ToString()).Path;
            var projectPath = Path.Combine(testDirectory, "project.csproj");
            File.WriteAllText(projectPath, "<Project />");
            var msbuildPath = Path.Combine(testDirectory, "custom msbuild.dll");
            File.WriteAllText(msbuildPath, "");

            string[] args = [projectPath, "--configuration", "Debug", .. separateRestore ? new[] { "--framework", "tfm" } : []];
            var command = (PublishCommand)PublishCommand.FromArgs(args, msbuildPath, TestCommandServices.CreateNonLLM());

            // MSBuild is a managed DLL, so the dotnet host executes the custom path.
            var expectedArgumentPrefix = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(["exec", msbuildPath]) + " ";
            var startInfo = command.GetProcessStartInfo();
            startInfo.FileName.Should().Be(new Muxer().MuxerPath);
            startInfo.Arguments.Should().StartWith(expectedArgumentPrefix);

            if (separateRestore)
            {
                command.SeparateRestoreCommand.Should().NotBeNull();
                var restoreStartInfo = command.SeparateRestoreCommand!.GetProcessStartInfo();
                restoreStartInfo.FileName.Should().Be(startInfo.FileName);
                restoreStartInfo.Arguments.Should().StartWith(expectedArgumentPrefix);
            }
            else
            {
                command.SeparateRestoreCommand.Should().BeNull();
            }
        }
    }
}
