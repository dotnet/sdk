// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests;

[TestClass]
public class GivenThatWeWantToUseProjectData : SdkTest
{
    [TestMethod]
    [CoreMSBuildOnly]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void It_preserves_project_data_targets_imported_by_a_host(bool multiTarget, bool innerBuild)
    {
        var testProject = new TestProject
        {
            Name = "HostProjectDataTest",
            TargetFrameworks = multiTarget ? $"{ToolsetInfo.CurrentTargetFramework};netstandard2.0" : ToolsetInfo.CurrentTargetFramework
        };
        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: $"{multiTarget}_{innerBuild}");
        string hostTargetsPath = Path.Combine(testAsset.Path, "HostProjectData.targets");
        string hostAssemblyPath = Path.Combine(testAsset.Path, "Host.ProjectData.Tasks.dll");
        File.Copy(
            Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "TestProjects", "ProjectDataHostTargets", "HostProjectData.targets"),
            hostTargetsPath);

        var arguments = new List<string>
        {
            "/p:DesignTimeBuild=true",
            "/p:BuildingProject=false",
            "/p:SkipCompilerExecution=true"
        };
        if (innerBuild)
        {
            arguments.Add($"/p:TargetFramework={ToolsetInfo.CurrentTargetFramework}");
        }

        new MSBuildCommand(testAsset, "ProjectDataBuild")
            .WithEnvironmentVariable("CustomAfterMicrosoftCommonTargets", hostTargetsPath)
            .WithEnvironmentVariable("CustomAfterMicrosoftCommonCrossTargetingTargets", hostTargetsPath)
            .Execute(arguments.ToArray())
            .Should().Pass();

        string markerPath = Path.Combine(testAsset.Path, testProject.Name, "host-projectdata.txt");
        File.Exists(markerPath).Should().BeTrue();
        File.ReadAllText(markerPath).Trim().Should().Be(hostAssemblyPath);
    }

    [TestMethod]
    [CoreMSBuildOnly]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void It_loads_the_bundled_tasks_and_only_writes_project_data_when_enabled(bool enabled, bool multiTarget)
    {
        var testProject = new TestProject
        {
            Name = "ProjectDataTest",
            TargetFrameworks = multiTarget ? $"{ToolsetInfo.CurrentTargetFramework};netstandard2.0" : ToolsetInfo.CurrentTargetFramework
        };
        testProject.AdditionalProperties["EnableProjectDataInProjectFolder"] = "true";
        if (enabled)
        {
            testProject.AdditionalProperties["EnableProjectDataOnBuild"] = "true";
        }

        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: $"{enabled}_{multiTarget}");

        new BuildCommand(testAsset).Execute().Should().Pass();

        string projectDataPath = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj.lscache");
        File.Exists(projectDataPath).Should().Be(enabled);
        if (enabled)
        {
            new FileInfo(projectDataPath).Length.Should().BeGreaterThan(0);
        }
    }
}
