// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ApiCompat.IntegrationTests
{
    /// <summary>
    /// End-to-end tests that drive the <c>apicompat</c> CLI tool by invoking
    /// <c>dotnet exec Microsoft.DotNet.ApiCompat.Tool.dll</c> with command-line arguments and
    /// asserting on the exit code and stdout, exactly as a customer would.
    /// </summary>
    public class ApiCompatToolIntegrationTests : SdkTest
    {
        private const string TestAssetName = "ApiCompatValidateAssembliesTestProject";

        public ApiCompatToolIntegrationTests(ITestOutputHelper log) : base(log)
        {
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiCompatTool_AssembliesIdentical_ExitsZero()
        {
            string assembly = BuildAsset(nameof(ApiCompatTool_AssembliesIdentical_ExitsZero), forceBreakingChange: false);

            var result = Run("--left", assembly, "--right", assembly);

            result.Should().Pass();
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiCompatTool_BreakingChange_ReportsCP0002()
        {
            string contractAssembly = BuildAsset($"{nameof(ApiCompatTool_BreakingChange_ReportsCP0002)}_left", forceBreakingChange: false);
            string implementationAssembly = BuildAsset($"{nameof(ApiCompatTool_BreakingChange_ReportsCP0002)}_right", forceBreakingChange: true);

            var result = Run("--left", contractAssembly, "--right", implementationAssembly);

            result.Should().Fail();
            string output = result.StdOut + result.StdErr;
            output.Should().Contain("CP0002")
                .And.Contain("Goodbye");
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiCompatTool_SuppressionFile_RoundTrip()
        {
            string contractAssembly = BuildAsset($"{nameof(ApiCompatTool_SuppressionFile_RoundTrip)}_left", forceBreakingChange: false);
            string implementationAssembly = BuildAsset($"{nameof(ApiCompatTool_SuppressionFile_RoundTrip)}_right", forceBreakingChange: true);

            string suppressionFile = Path.Combine(Path.GetDirectoryName(implementationAssembly)!, "suppressions.xml");

            // 1) Generate the suppression file
            var generateResult = Run(
                "--left", contractAssembly,
                "--right", implementationAssembly,
                "--generate-suppression-file",
                "--suppression-output-file", suppressionFile);

            generateResult.Should().Pass();
            File.Exists(suppressionFile).Should().BeTrue("suppression file should have been written");
            File.ReadAllText(suppressionFile).Should().Contain("CP0002");

            // 2) Re-run consuming the suppression file; the diff is now suppressed
            var consumeResult = Run(
                "--left", contractAssembly,
                "--right", implementationAssembly,
                "--suppression-file", suppressionFile);

            consumeResult.Should().Pass();
            consumeResult.StdOut.Should().NotContain("error CP0002");
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiCompatTool_PackageMode_DetectsRemovedApi()
        {
            // Pack the existing PackageValidationTestProject twice to produce two .nupkg files
            // that differ by a removed public API, then compare them with `apicompat package`.
            string baselinePackage = PackPackageValidationTestProject(
                $"{nameof(ApiCompatTool_PackageMode_DetectsRemovedApi)}_baseline",
                addBreakingChange: false);
            string newerPackage = PackPackageValidationTestProject(
                $"{nameof(ApiCompatTool_PackageMode_DetectsRemovedApi)}_newer",
                addBreakingChange: true,
                packageVersion: "2.0.0");

            var result = Run("package", newerPackage, "--baseline-package", baselinePackage);

            result.Should().Fail();
            string output = result.StdOut + result.StdErr;
            output.Should().Contain("CP0002")
                .And.Contain("SomeApiNotInLatestVersion");
        }

        private CommandResult Run(params string[] args)
        {
            var allArgs = new List<string> { "exec", ToolPaths.ApiCompatToolDll };
            allArgs.AddRange(args);
            return new DotnetCommand(Log, allArgs.ToArray()).Execute();
        }

        /// <summary>
        /// Builds a copy of the test asset and returns the absolute path to the produced assembly.
        /// </summary>
        private string BuildAsset(string identifier, bool forceBreakingChange)
        {
            TestAsset asset = TestAssetsManager
                .CopyTestAsset(TestAssetName, identifier: identifier)
                .WithSource();

            var args = forceBreakingChange ? new[] { "-p:ForceBreakingChange=true" } : Array.Empty<string>();
            new BuildCommand(asset).Execute(args).Should().Pass();

            return Path.Combine(asset.TestRoot, "bin", "Debug",
                ToolsetInfo.CurrentTargetFramework, $"{TestAssetName}.dll");
        }

        private string PackPackageValidationTestProject(string identifier, bool addBreakingChange, string packageVersion = "1.0.0")
        {
            TestAsset asset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", identifier: identifier)
                .WithSource();

            var packageOutputPath = Path.Combine(asset.TestRoot, "pkg");
            Directory.CreateDirectory(packageOutputPath);

            var args = new List<string>
            {
                "-p:EnablePackageValidation=false",
                $"-p:PackageOutputPath={packageOutputPath}",
                $"-p:PackageVersion={packageVersion}",
            };
            if (addBreakingChange)
            {
                args.Add("-p:AddBreakingChange=true");
            }

            new PackCommand(Log, Path.Combine(asset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute(args.ToArray()).Should().Pass();

            return Path.Combine(packageOutputPath, $"PackageValidationTestProject.{packageVersion}.nupkg");
        }
    }
}
