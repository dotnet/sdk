// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.GenAPI.IntegrationTests.Task
{
    /// <summary>
    /// End-to-end tests that drive the GenAPI MSBuild task by adding a PackageReference to
    /// <c>Microsoft.DotNet.GenAPI.Task</c> from the local testpackages feed and invoking
    /// <c>dotnet build /t:GenAPIGenerateReferenceAssemblySource</c> against a real test project.
    /// </summary>
    public class GenAPITaskIntegrationTests(ITestOutputHelper log) : SdkTest(log)
    {
        private const string TestAssetName = "GenAPITaskTestProject";

        [Fact]
        public void GenAPITask_GeneratesReferenceSource_OnBuild()
        {
            TestAsset asset = PrepareAsset(nameof(GenAPITask_GeneratesReferenceSource_OnBuild));

            new BuildCommand(asset)
                .Execute(
                    "-p:GenAPIGenerateReferenceAssemblySource=true",
                    $"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}")
                .Should().Pass();

            string generated = Path.Combine(asset.TestRoot, "bin", "Debug",
                ToolsetInfo.CurrentTargetFramework, $"{TestAssetName}.cs");
            File.Exists(generated).Should().BeTrue($"GenAPI should have produced {generated}");
            string contents = File.ReadAllText(generated);
            contents.Should().Contain("namespace GenAPITaskTestProject")
                .And.Contain("public partial class Calculator")
                .And.Contain("Add")
                .And.Contain("Subtract")
                .And.NotContain("InternalMultiply", "internal members should not leak into the reference source by default");
        }

        [Fact]
        public void GenAPITask_TargetInvokedDirectly_DoesNotBuildProjectReferences()
        {
            // Customer scenario: invoke /t:GenAPIGenerateReferenceAssemblySource directly without
            // GenAPIGenerateReferenceAssemblySource=true. The targets file sets BuildProjectReferences=false
            // in this mode so transitive project references are skipped.
            TestAsset asset = PrepareAsset(nameof(GenAPITask_TargetInvokedDirectly_DoesNotBuildProjectReferences));

            // First build to produce the IntermediateAssembly that GenAPI consumes.
            new BuildCommand(asset)
                .Execute($"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}")
                .Should().Pass();

            new MSBuildCommand(asset, "GenAPIGenerateReferenceAssemblySource")
                .Execute()
                .Should().Pass();

            string generated = Path.Combine(asset.TestRoot, "bin", "Debug",
                ToolsetInfo.CurrentTargetFramework, $"{TestAssetName}.cs");
            File.Exists(generated).Should().BeTrue();
        }

        [Fact]
        public void GenAPITask_RespectInternals_IncludesInternalMembers()
        {
            TestAsset asset = PrepareAsset(nameof(GenAPITask_RespectInternals_IncludesInternalMembers));

            new BuildCommand(asset)
                .Execute(
                    "-p:GenAPIGenerateReferenceAssemblySource=true",
                    "-p:GenAPIRespectInternals=true",
                    $"-p:RestoreAdditionalProjectSources={SdkTestContext.Current.TestPackages}")
                .Should().Pass();

            string generated = Path.Combine(asset.TestRoot, "bin", "Debug",
                ToolsetInfo.CurrentTargetFramework, $"{TestAssetName}.cs");
            File.ReadAllText(generated).Should().Contain("InternalMultiply",
                "GenAPIRespectInternals=true should expose internal members in the generated reference source");
        }

        private TestAsset PrepareAsset(string testName)
        {
            TestAsset asset = TestAssetsManager
                .CopyTestAsset(TestAssetName, identifier: testName)
                .WithSource();

            new DotnetCommand(Log, "add", asset.Path, "package", "Microsoft.DotNet.GenAPI.Task",
                "--prerelease", "--source", SdkTestContext.Current.TestPackages)
                .Execute()
                .Should().Pass();
            return asset;
        }
    }
}
