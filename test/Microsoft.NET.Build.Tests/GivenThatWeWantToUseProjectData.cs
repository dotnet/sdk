// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests;

[TestClass]
public class GivenThatWeWantToUseProjectData : SdkTest
{
    [TestMethod]
    [CoreMSBuildOnly]
    [DataRow(false)]
    [DataRow(true)]
    public void It_loads_the_bundled_tasks_and_only_writes_project_data_when_enabled(bool enabled)
    {
        var testProject = new TestProject
        {
            Name = "ProjectDataTest",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework
        };
        testProject.AdditionalProperties["EnableProjectDataInProjectFolder"] = "true";
        if (enabled)
        {
            testProject.AdditionalProperties["EnableProjectDataOnBuild"] = "true";
        }

        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: enabled.ToString())
            .WithProjectChanges(project =>
            {
                project.Root!.Attribute("Sdk")!.Remove();
                project.Root.AddFirst(new XElement("Import",
                    new XAttribute("Project", "Sdk.props"),
                    new XAttribute("Sdk", "Microsoft.NET.Sdk")));
                project.Root.Add(
                    new XElement("Import",
                        new XAttribute("Project", "Sdk.targets"),
                        new XAttribute("Sdk", "Microsoft.NET.Sdk")),
                    new XElement("Import",
                        new XAttribute("Project", "$(MSBuildSDKsPath)/../ProjectData/build/Microsoft.NET.ProjectData.targets")));
            });

        new BuildCommand(testAsset).Execute().Should().Pass();

        string projectDataPath = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj.lscache");
        File.Exists(projectDataPath).Should().Be(enabled);
        if (enabled)
        {
            new FileInfo(projectDataPath).Length.Should().BeGreaterThan(0);
        }
    }
}
