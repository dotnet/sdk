// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.NET.Sdk.Web.Tests;

[TestClass]
public class PublishOptOutTests : SdkTest
{
    [TestMethod]
    [CombinatorialData]
    public void Nonpublishable_projects_skip_publish_hooks(
        [CombinatorialValues("Microsoft.NET.Sdk.Web", "Microsoft.NET.Sdk.Worker")] string sdk,
        [CombinatorialValues("project", "commandLine", "profile")] string propertySource,
        bool deployOnBuild)
    {
        var testProject = CreateProject("Nonpublishable", sdk);
        if (propertySource == "project")
        {
            testProject.AdditionalProperties["IsPublishable"] = "false";
        }

        var testAsset = TestAssetsManager.CreateTestProject(
            testProject, identifier: $"{sdk}_{propertySource}_{deployOnBuild}");
        string projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name!);
        var arguments = new List<string>();
        if (propertySource == "commandLine")
        {
            arguments.Add("-p:IsPublishable=false");
        }
        else if (propertySource == "profile")
        {
            WriteProfile(projectDirectory, isPublishable: false);
            arguments.Add("-p:PublishProfile=FolderProfile");
        }

        ExecutePublish(projectDirectory, deployOnBuild, arguments);

        AssertNoPublishSideEffects(projectDirectory);
        File.Exists(Path.Combine(projectDirectory, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{testProject.Name}.dll"))
            .Should().Be(deployOnBuild);
    }

    [TestMethod]
    [CombinatorialData]
    public void Mixed_solution_only_publishes_publishable_projects(bool deployOnBuild, bool useProfile)
    {
        var publishable = CreateProject("Publishable", "Microsoft.NET.Sdk.Web");
        var nonpublishable = CreateProject("Nonpublishable", "Microsoft.NET.Sdk.Web");
        nonpublishable.AdditionalProperties["IsPublishable"] = "false";
        var worker = CreateProject("NonpublishableWorker", "Microsoft.NET.Sdk.Worker");
        worker.AdditionalProperties["IsPublishable"] = "false";
        var projects = new[] { publishable, nonpublishable, worker };
        var testAsset = TestAssetsManager.CreateTestProjects(
            projects, identifier: $"{deployOnBuild}_{useProfile}");
        var arguments = new List<string>();
        if (useProfile)
        {
            foreach (var project in projects)
            {
                WriteProfile(Path.Combine(testAsset.TestRoot, project.Name!));
            }

            arguments.Add("-p:PublishProfile=FolderProfile");
        }

        ExecutePublish(testAsset.TestRoot, deployOnBuild, arguments);

        string publishableDirectory = Path.Combine(testAsset.TestRoot, publishable.Name!);
        new DirectoryInfo(Path.Combine(publishableDirectory, "publish"))
            .Should().HaveFiles(["Publishable.dll", "web.config"]);
        File.Exists(Path.Combine(publishableDirectory, "publish-hooks.txt")).Should().BeTrue();
        if (useProfile)
        {
            File.ReadAllText(Path.Combine(publishableDirectory, "publish", "web.config"))
                .Should().Contain("ProfileEnvironment");
        }

        AssertNoPublishSideEffects(Path.Combine(testAsset.TestRoot, nonpublishable.Name!));
        AssertNoPublishSideEffects(Path.Combine(testAsset.TestRoot, worker.Name!));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Nonpublishable_projects_leave_existing_publish_output_untouched(bool deployOnBuild)
    {
        var testProject = CreateProject("ExistingOutput", "Microsoft.NET.Sdk.Web");
        testProject.AdditionalProperties["IsPublishable"] = "false";
        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: deployOnBuild.ToString());
        string projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name!);
        string publishDirectory = Path.Combine(projectDirectory, "publish");
        Directory.CreateDirectory(publishDirectory);
        string webConfig = Path.Combine(publishDirectory, "web.config");
        const string originalContents = "<configuration />";
        File.WriteAllText(webConfig, originalContents);
        DateTime originalWriteTime = File.GetLastWriteTimeUtc(webConfig);

        WriteProfile(projectDirectory);
        ExecutePublish(projectDirectory, deployOnBuild, ["-p:PublishProfile=FolderProfile"]);

        File.ReadAllText(webConfig).Should().Be(originalContents);
        File.GetLastWriteTimeUtc(webConfig).Should().Be(originalWriteTime);
        new DirectoryInfo(publishDirectory).Should().OnlyHaveFiles(["web.config"]);
        AssertNoPublishSideEffects(projectDirectory, existingPublishDirectory: true);
    }

    private static TestProject CreateProject(string name, string sdk)
    {
        var project = new TestProject
        {
            Name = name,
            ProjectSdk = sdk,
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true,
        };
        project.AdditionalProperties["PublishDir"] = @"publish\";
        project.AdditionalProperties["PublishUrl"] = @"$(MSBuildProjectDirectory)\publish\";
        project.ProjectChanges.Add(document => document.Root!.Add(XElement.Parse("""
            <Target Name="RecordPublishHooks"
                    BeforeTargets="BeforePublish;AfterPublish;_DetermineProjectType;_InitPublishIntermediateOutputPath;_TransformWebConfig;_TransformXml;_TransformAppSettings;_GenerateEFSQLScripts;_GenerateRunCommandFile;_PublishFiles">
              <WriteLinesToFile File="$(MSBuildProjectDirectory)\publish-hooks.txt" Lines="Publish hooks ran" />
            </Target>
            """)));
        return project;
    }

    private static void WriteProfile(string projectDirectory, bool? isPublishable = null)
    {
        string profileDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
        Directory.CreateDirectory(profileDirectory);
        File.WriteAllText(Path.Combine(profileDirectory, "FolderProfile.pubxml"), $"""
            <Project>
              <PropertyGroup>
                <WebPublishMethod>FileSystem</WebPublishMethod>
                <DeleteExistingFiles>true</DeleteExistingFiles>
                <EnvironmentName>ProfileEnvironment</EnvironmentName>
                {(isPublishable.HasValue ? $"<IsPublishable>{isPublishable.Value.ToString().ToLowerInvariant()}</IsPublishable>" : "")}
              </PropertyGroup>
            </Project>
            """);
    }

    private void ExecutePublish(string directory, bool deployOnBuild, IEnumerable<string> arguments)
    {
        if (deployOnBuild)
        {
            new DotnetBuildCommand(Log, directory)
                .Execute(arguments.Concat([BinLogArgument([]), "-p:DeployOnBuild=true", "-p:Configuration=Debug"]))
                .Should().Pass();
        }
        else
        {
            new DotnetPublishCommand(Log, directory)
                .Execute(arguments.Concat([BinLogArgument([]), "-p:Configuration=Debug"]))
                .Should().Pass();
        }
    }

    private static void AssertNoPublishSideEffects(string projectDirectory, bool existingPublishDirectory = false)
    {
        Directory.Exists(Path.Combine(projectDirectory, "publish")).Should().Be(existingPublishDirectory);
        Directory.Exists(Path.Combine(projectDirectory, "obj", "Debug", ToolsetInfo.CurrentTargetFramework, "PubTmp"))
            .Should().BeFalse();
        File.Exists(Path.Combine(projectDirectory, "publish-hooks.txt")).Should().BeFalse();
    }
}
