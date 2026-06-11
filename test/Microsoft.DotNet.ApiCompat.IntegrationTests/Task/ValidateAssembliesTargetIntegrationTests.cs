// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompat.Task.IntegrationTests
{
    public class ValidateAssembliesTargetIntegrationTests(ITestOutputHelper log) : SdkTest(log)
    {
        private const string TestAssetName = "ApiCompatValidateAssembliesTestProject";

        [Fact]
        public void ValidateAssemblies_NoBreakingChanges_Succeeds()
        {
            // Build the asset twice with the same source to produce a "contract" DLL and an "implementation" DLL
            // that are byte-for-byte equivalent. ApiCompat should report no errors.
            string contractAssembly = BuildContractAssembly(nameof(ValidateAssemblies_NoBreakingChanges_Succeeds));

            TestAsset implementationAsset = PrepareImplementationAsset(nameof(ValidateAssemblies_NoBreakingChanges_Succeeds));
            var result = new BuildCommand(implementationAsset)
                .Execute(
                    "-p:ApiCompatValidateAssemblies=true",
                    $"-p:ApiCompatContractAssembly={contractAssembly}",
                    $"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}");

            result.Should().Pass();
            result.StdOut.Should().NotContain("error CP0002");
        }

        [Fact]
        public void ValidateAssemblies_BreakingChange_FailsWithCP0002()
        {
            // Contract has Goodbye(string); implementation removes it via -p:ForceBreakingChange=true.
            // ApiCompat should report a CP0002 error and the build should fail.
            string contractAssembly = BuildContractAssembly(nameof(ValidateAssemblies_BreakingChange_FailsWithCP0002));

            TestAsset implementationAsset = PrepareImplementationAsset(nameof(ValidateAssemblies_BreakingChange_FailsWithCP0002));
            var result = new BuildCommand(implementationAsset)
                .Execute(
                    "-p:ApiCompatValidateAssemblies=true",
                    "-p:ForceBreakingChange=true",
                    $"-p:ApiCompatContractAssembly={contractAssembly}",
                    $"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}");

            result.Should().Fail();
            result.StdOut.Should().Contain("error CP0002")
                .And.Contain("Goodbye");
        }

        [Fact]
        public void ValidateAssemblies_StrictMode_FailsOnAddition()
        {
            // Implementation adds Welcome(string) (-p:AddNewMember=true). Without strict mode, ApiCompat tolerates additions.
            // With ApiCompatStrictMode=true, the addition is also reported as a CP0002 error.
            string contractAssembly = BuildContractAssembly(nameof(ValidateAssemblies_StrictMode_FailsOnAddition));

            TestAsset implementationAsset = PrepareImplementationAsset(nameof(ValidateAssemblies_StrictMode_FailsOnAddition));
            var result = new BuildCommand(implementationAsset)
                .Execute(
                    "-p:ApiCompatValidateAssemblies=true",
                    "-p:ApiCompatStrictMode=true",
                    "-p:AddNewMember=true",
                    $"-p:ApiCompatContractAssembly={contractAssembly}",
                    $"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}");

            result.Should().Fail();
            result.StdOut.Should().Contain("error CP0002")
                .And.Contain("Welcome");
        }

        [Fact]
        public void ValidateAssemblies_GeneratesAndConsumesSuppressionFile()
        {
            // 1) Generate a suppression file for the breaking change.
            // 2) Re-run with the suppression file in place; the error should be suppressed and the build should pass.
            string contractAssembly = BuildContractAssembly(nameof(ValidateAssemblies_GeneratesAndConsumesSuppressionFile));

            TestAsset implementationAsset = PrepareImplementationAsset(nameof(ValidateAssemblies_GeneratesAndConsumesSuppressionFile));
            string suppressionFile = Path.Combine(implementationAsset.TestRoot, "CompatibilitySuppressions.xml");

            var generateResult = new BuildCommand(implementationAsset)
                .Execute(
                    "-p:ApiCompatValidateAssemblies=true",
                    "-p:ForceBreakingChange=true",
                    "-p:ApiCompatGenerateSuppressionFile=true",
                    $"-p:ApiCompatContractAssembly={contractAssembly}",
                    $"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}");
            generateResult.Should().Pass();
            File.Exists(suppressionFile).Should().BeTrue("the suppression file should have been written");
            File.ReadAllText(suppressionFile).Should().Contain("CP0002");

            var consumeResult = new BuildCommand(implementationAsset)
                .Execute(
                    "-p:ApiCompatValidateAssemblies=true",
                    "-p:ForceBreakingChange=true",
                    $"-p:ApiCompatContractAssembly={contractAssembly}",
                    $"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}");
            consumeResult.Should().Pass();
            consumeResult.StdOut.Should().NotContain("error CP0002");
        }

        /// <summary>
        /// Builds a copy of the test asset with no breaking-change defines to produce the "contract" assembly,
        /// then preserves it outside the bin folder so a subsequent build (used as the "implementation") can compare against it.
        /// </summary>
        private string BuildContractAssembly(string testName)
        {
            TestAsset contractAsset = TestAssetsManager
                .CopyTestAsset(TestAssetName, identifier: $"{testName}_contract")
                .WithSource();

            new BuildCommand(contractAsset).Execute().Should().Pass();

            string built = Path.Combine(contractAsset.TestRoot, "bin", "Debug",
                ToolsetInfo.CurrentTargetFramework, $"{TestAssetName}.dll");
            string preserved = Path.Combine(contractAsset.TestRoot, "contract", $"{TestAssetName}.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(preserved)!);
            File.Copy(built, preserved, overwrite: true);
            return preserved;
        }

        /// <summary>
        /// Copies a fresh asset and adds the Microsoft.DotNet.ApiCompat.Task PackageReference (from the local
        /// testpackages feed) so the implementation build has the ValidateAssemblies target wired up.
        /// </summary>
        private TestAsset PrepareImplementationAsset(string testName)
        {
            TestAsset asset = TestAssetsManager
                .CopyTestAsset(TestAssetName, identifier: $"{testName}_impl")
                .WithSource();

            new DotnetCommand(Log, "add", asset.Path, "package", "Microsoft.DotNet.ApiCompat.Task",
                "--prerelease", "--source", SdkTestContext.Current.TestPackages)
                .Execute()
                .Should().Pass();
            return asset;
        }
    }
}

