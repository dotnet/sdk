// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.GenAPI.IntegrationTests.Tool
{
    /// <summary>
    /// End-to-end tests that drive the <c>genapi</c> CLI tool by invoking
    /// <c>dotnet exec Microsoft.DotNet.GenAPI.Tool.dll</c> against a real built assembly and
    /// asserting on the produced reference source, exactly as a customer would.
    /// </summary>
    public class GenAPIToolIntegrationTests : SdkTest
    {
        private const string TestAssetName = "GenAPITaskTestProject";

        public GenAPIToolIntegrationTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void GenAPITool_GeneratesSourceForAssembly()
        {
            (string assembly, string outputDirectory) = BuildAndPrepareOutput(nameof(GenAPITool_GeneratesSourceForAssembly));

            Run("--assembly", assembly, "--output-path", outputDirectory).Should().Pass();

            string generated = Path.Combine(outputDirectory, $"{TestAssetName}.cs");
            File.Exists(generated).Should().BeTrue();
            string contents = File.ReadAllText(generated);
            contents.Should().Contain("public partial class Calculator")
                .And.Contain("Add")
                .And.Contain("Subtract")
                .And.NotContain("InternalMultiply");
        }

        [Fact]
        public void GenAPITool_HeaderFile_IsPrependedToOutput()
        {
            (string assembly, string outputDirectory) = BuildAndPrepareOutput(nameof(GenAPITool_HeaderFile_IsPrependedToOutput));

            string headerFile = Path.Combine(outputDirectory, "header.txt");
            string headerLine = "// Custom GenAPI header";
            File.WriteAllText(headerFile, headerLine + Environment.NewLine);

            Run("--assembly", assembly, "--output-path", outputDirectory, "--header-file", headerFile).Should().Pass();

            string generated = Path.Combine(outputDirectory, $"{TestAssetName}.cs");
            File.ReadAllText(generated).Should().StartWith(headerLine);
        }

        [Fact]
        public void GenAPITool_RespectInternals_IncludesInternalMembers()
        {
            (string assembly, string outputDirectory) = BuildAndPrepareOutput(nameof(GenAPITool_RespectInternals_IncludesInternalMembers));

            Run("--assembly", assembly, "--output-path", outputDirectory, "--respect-internals").Should().Pass();

            string generated = Path.Combine(outputDirectory, $"{TestAssetName}.cs");
            File.ReadAllText(generated).Should().Contain("InternalMultiply");
        }

        private CommandResult Run(params string[] args)
        {
            // The tool DLL targets $(NetMinimum) (net10.0). On macOS arm64 the helix payload's bundled
            // Microsoft.NETCore.App/10.0.0 is x86_64-only, so dlopen of libhostpolicy.dylib fails. We
            // need to roll forward to the redist SDK's net11.0 runtime — but it's a prerelease (e.g.
            // 11.0.0-preview.x), and roll-forward refuses prereleases by default. Pass --roll-forward
            // LatestMajor (must follow the `exec` verb) and DOTNET_ROLL_FORWARD_TO_PRERELEASE=1.
            var allArgs = new List<string> { "exec", "--roll-forward", "LatestMajor", ToolPaths.GenAPIToolDll };
            allArgs.AddRange(args);
            return new DotnetCommand(Log, allArgs.ToArray())
                .WithEnvironmentVariable("DOTNET_ROLL_FORWARD_TO_PRERELEASE", "1")
                .Execute();
        }

        /// <summary>
        /// Builds a copy of the test asset and returns the assembly path together with a fresh,
        /// per-test output directory for the generated reference source.
        /// </summary>
        private (string assembly, string outputDirectory) BuildAndPrepareOutput(string identifier)
        {
            TestAsset asset = TestAssetsManager
                .CopyTestAsset(TestAssetName, identifier: identifier)
                .WithSource();

            new BuildCommand(asset).Execute().Should().Pass();

            string assembly = Path.Combine(asset.TestRoot, "bin", "Debug",
                ToolsetInfo.CurrentTargetFramework, $"{TestAssetName}.dll");
            string outputDirectory = Path.Combine(asset.TestRoot, "ref");
            Directory.CreateDirectory(outputDirectory);
            return (assembly, outputDirectory);
        }
    }
}
