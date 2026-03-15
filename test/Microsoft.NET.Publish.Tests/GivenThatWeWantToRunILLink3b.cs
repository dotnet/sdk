// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Newtonsoft.Json.Linq;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;
using static Microsoft.NET.Publish.Tests.ILLinkTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    // this test class is split up arbitrarily so Helix can run tests in multiple workitems
    public class GivenThatWeWantToRunILLink3b : SdkTest
    {
        public GivenThatWeWantToRunILLink3b(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_treat_warnings_as_errors_independently(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:TreatWarningsAsErrors=true", "/p:ILLinkTreatWarningsAsErrors=false", "/p:EnableTrimAnalyzer=false")
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL2046")
                .And.HaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("error IL2026")
                .And.NotHaveStdOutContaining("error IL2046")
                .And.NotHaveStdOutContaining("error IL2075");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("net5.0")]
        [InlineData("netcoreapp3.1")]
        public void ILLink_displays_informational_warning_up_to_net5_by_default(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("https://aka.ms/dotnet-illink");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_displays_informational_warning_when_trim_analysis_warnings_are_suppressed_on_net6plus(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}", "/p:SuppressTrimAnalysisWarnings=true")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("https://aka.ms/dotnet-illink")
                .And.HaveStdOutContainingIgnoreCase("This process might take a while");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_dont_display_informational_warning_by_default_on_net6plus(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.NotHaveStdErrContaining("https://aka.ms/dotnet-illink")
                .And.HaveStdOutContainingIgnoreCase("This process might take a while");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_dont_display_time_awareness_message_on_incremental_build(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("This process might take a while");

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.NotHaveStdErrContaining("This process might take a while");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false)]
        public void Build_respects_IsTrimmable_property(string targetFramework, bool isExe)
        {
            var projectName = "AnalysisWarnings";

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, isExe);
            testProject.AdditionalProperties["IsTrimmable"] = "true";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            testProject.AdditionalProperties["RuntimeIdentifier"] = rid;
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework + isExe);

            var buildCommand = new BuildCommand(testAsset);
            // IsTrimmable enables analysis warnings during build
            buildCommand.Execute()
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}.dll");
            var runtimeConfigPath = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");

            // injects the IsTrimmable attribute
            AssemblyInfo.Get(assemblyPath).Should().Contain(("AssemblyMetadataAttribute", "IsTrimmable:True"));

            // just setting IsTrimmable doesn't enable feature settings
            // (these only affect apps, and wouldn't make sense for libraries either)
            if (isExe)
            {
                JObject runtimeConfig = JObject.Parse(File.ReadAllText(runtimeConfigPath));
                JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];
                if (configProperties != null)
                    configProperties["System.StartupHookProvider.IsSupported"].Should().BeNull();
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void Build_respects_PublishTrimmed_property(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            // PublishTrimmed enables analysis warnings during build
            buildCommand.Execute($"-p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}.dll");
            var runtimeConfigPath = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");

            // runtimeconfig has trim settings
            JObject runtimeConfig = JObject.Parse(File.ReadAllText(runtimeConfigPath));
            JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];

            configProperties["System.StartupHookProvider.IsSupported"].Value<bool>().Should().BeFalse();

            // Build with PublishTrimmed enabled should disable System.Text.Json reflection
            configProperties["System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault"].Value<bool>().Should().BeFalse();

            // just setting PublishTrimmed doesn't inject the IsTrimmable attribute
            AssemblyInfo.Get(assemblyPath).Should().NotContain(i => i.Key == "AssemblyMetadataAttribute");
        }
    }
}
