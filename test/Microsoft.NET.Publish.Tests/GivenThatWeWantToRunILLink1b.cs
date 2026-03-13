// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.Build.Tasks;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;
using static Microsoft.NET.Publish.Tests.ILLinkTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    // this test class is split up arbitrarily so Helix can run tests in multiple workitems
    public class GivenThatWeWantToRunILLink1b : SdkTest
    {
        public GivenThatWeWantToRunILLink1b(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_IsTrimmable_metadata_can_override_attribute(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetGlobalTrimMode(project, "partial"))
                .WithProjectChanges(project => SetMetadata(project, "UnusedTrimmableAssembly", "IsTrimmable", "false"))
                .WithProjectChanges(project => SetMetadata(project, "UnusedNonTrimmableAssembly", "IsTrimmable", "true"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/v:n").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Attributed IsTrimmable assembly with IsTrimmable=false metadata should be kept
            DoesImageHaveMethod(unusedTrimmableDll, "UnusedMethod").Should().BeTrue();
            // Unattributed assembly with IsTrimmable=true should be trimmed
            File.Exists(unusedNonTrimmableDll).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("net6.0")]
        public void ILLink_TrimMode_applies_to_IsTrimmable_assemblies(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(trimmableDll, "UsedMethod").Should().BeTrue();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            // Non-trimmable assemblies still get copied
            DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeTrue();
            DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "full")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "partial")]
        public void ILLink_TrimMode_new_options(string targetFramework, string trimMode)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework + trimMode)
                .WithProjectChanges(project => SetGlobalTrimMode(project, trimMode));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "-bl").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(trimmableDll, "UsedMethod").Should().BeTrue();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            if (trimMode is "full")
            {
                DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeFalse();
                File.Exists(unusedNonTrimmableDll).Should().BeFalse();
            }
            else if (trimMode is "partial")
            {
                DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeTrue();
                DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
            }
            else
            {
                Assert.Fail("unexpected value");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_can_set_TrimmerDefaultAction(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetTrimmerDefaultAction(project, "link"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            // Unattributed assemblies are trimmed at member level
            DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeFalse();
            File.Exists(unusedNonTrimmableDll).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("net5.0")]
        public void ILLink_analysis_warnings_are_disabled_by_default(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                // trim analysis warnings are disabled
                .And.NotHaveStdOutMatching(@"warning IL\d\d\d\d");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  ILLINK : Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/7.0.0/libhostpolicy.dylib, error : dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/7.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/7.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')),
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_analysis_warnings_are_enabled_by_default(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            // Minimal verbosity prevents desktop MSBuild.exe from duplicating the warnings in the warning summary
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/v:m");
            result.Should().Pass();
            // trim analysis warnings are enabled
            var expectedWarnings = new List<string> {
                // ILLink warnings
                "Trim analysis warning IL2075.*Program.IL_2075",
                "Trim analysis warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026",
                "Trim analysis warning IL2043.*Program.IL_2043.get",
                "Trim analysis warning IL2026.*Program.Base.IL_2046",
                "Trim analysis warning IL2046.*Program.Derived.IL_2046",
                "Trim analysis warning IL2093.*Program.Derived.IL_2093",
                // analyzer warnings
                "warning IL2075.*Type.GetMethod",
                "warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026",
                "warning IL2043.*Program.IL_2043.get",
                "warning IL2026.*Program.Base.IL_2046",
                "warning IL2046.*Program.Derived.IL_2046",
                "warning IL2093.*Program.Derived.IL_2093"
            };
            ValidateWarningsOnHelloWorldApp(publishCommand, result, expectedWarnings, targetFramework, rid, useRegex: true);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_enable_analysis_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2075.*Program.IL_2075")
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026")
                .And.HaveStdOutMatching("warning IL2043.*Program.IL_2043.get")
                .And.HaveStdOutMatching("warning IL2046.*Program.Derived.IL_2046")
                .And.HaveStdOutMatching("warning IL2093.*Program.Derived.IL_2093");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_disable_analysis_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "true";
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("warning IL2026")
                .And.NotHaveStdOutContaining("warning IL2043")
                .And.NotHaveStdOutContaining("warning IL2046")
                .And.NotHaveStdOutContaining("warning IL2093");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_enable_analysis_warnings_without_PublishTrimmed(string targetFramework)
        {
            if (targetFramework == "net5.0" &&
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  https://github.com/dotnet/sdk/issues/49665
                return;
            }

            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["EnableTrimAnalyzer"] = "true";
            testProject.AdditionalProperties["EnableNETAnalyzers"] = "false";
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            // Minimal verbosity prevents desktop MSBuild.exe from duplicating the warnings in the warning summary
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/v:m");
            result.Should().Pass();
            var expectedWarnings = new List<string> {
                // analyzer warnings
                "warning IL2075.*Type.GetMethod",
                "warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026",
                "warning IL2043.*Program.IL_2043.get",
                "warning IL2026.*Program.Base.IL_2046",
                "warning IL2046.*Program.Derived.IL_2046",
                "warning IL2093.*Program.Derived.IL_2093"
            };
            ValidateWarningsOnHelloWorldApp(publishCommand, result, expectedWarnings, targetFramework, rid, useRegex: true);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_shows_single_warning_for_packagereferences_only(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = TestAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource();

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.HaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.NotHaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.HaveStdOutMatching("IL2104.*'PackageReference'")
                .And.NotHaveStdOutMatching("IL2104.*'App'")
                .And.NotHaveStdOutMatching("IL2104.*'ProjectReference'")
                .And.NotHaveStdOutMatching("IL2104.*'TransitiveProjectReference'");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_accepts_option_to_show_all_warnings(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = TestAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource();

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:TrimmerSingleWarn=false")
                .Should().Pass()
                .And.HaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.HaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.NotHaveStdOutContaining("IL2104");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_can_show_single_warning_per_assembly(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = TestAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    SetMetadata(project, "PackageReference", "TrimmerSingleWarn", "false");
                    SetMetadata(project, "ProjectReference", "TrimmerSingleWarn", "true");
                    SetMetadata(project, "App", "TrimmerSingleWarn", "true");
                });

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:TrimmerSingleWarn=false")
                .Should().Pass()
                .And.NotHaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.NotHaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.NotHaveStdOutMatching("IL2104.*'PackageReference'")
                .And.HaveStdOutMatching("IL2104.*'App'")
                .And.HaveStdOutMatching("IL2104.*'ProjectReference'")
                .And.NotHaveStdOutMatching("IL2104.*'TransitiveProjectReference'");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_errors_fail_the_build(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            // Set up a project with an invalid feature substitution, just to produce an error.
            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName);
            testProject.SourceFiles[$"{projectName}.xml"] = $@"
<linker>
  <assembly fullname=""{projectName}"">
    <type fullname=""Program"" feature=""featuremissingvalue"" />
  </assembly>
</linker>
";
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => AddRootDescriptor(project, $"{projectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false")
                .Should().Fail()
                .And.HaveStdOutContaining("error IL1001")
                .And.HaveStdOutContaining(Strings.ILLinkFailed);

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");

            File.Exists(linkSemaphore).Should().BeFalse();
            File.Exists(publishedDll).Should().BeFalse();
        }
    }
}
