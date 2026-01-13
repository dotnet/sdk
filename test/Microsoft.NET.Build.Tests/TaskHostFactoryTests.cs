// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;

namespace Microsoft.NET.Build.Tests;

/// <summary>
/// Tests to ensure MSBuild tasks declared with TaskHostFactory work correctly.
/// TaskHostFactory enables tasks to run out-of-process, which is important for
/// cross-runtime scenarios (e.g., .NET Core tasks from .NET Framework MSBuild).
/// </summary>
public class TaskHostFactoryTests(ITestOutputHelper log) : SdkTest(log)
{
    /// <summary>
    /// Verifies that TaskHostFactory with Runtime="NET" attribute executes successfully
    /// across all supported target frameworks. The Runtime attribute specifies which
    /// runtime the task should run on.
    /// </summary>
    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    public void TaskWithTaskHostFactory_RuntimeNET_ExecutesSuccessfully(string targetFramework)
    {
        var testAsset = CreateTestAssetWithTaskHostFactoryAndRuntime("NET", targetFramework);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand.Execute("/t:Build;TestTaskHostFactoryWithRuntime", "/v:diag")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory with Runtime=NET executed successfully");
    }

    /// <summary>
    /// Verifies that TaskHostFactory with Runtime="CLR4" attribute executes successfully
    /// when running from Full MSBuild (Visual Studio). The CLR4 runtime specifies that
    /// the task should run on .NET Framework 4.x CLR.
    /// </summary>
    /// <remarks>
    /// This test validates the VS/Full MSBuild scenario where tasks need to run in the
    /// .NET Framework task host. This is the primary scenario for Visual Studio builds.
    /// </remarks>
    [FullMSBuildOnlyTheory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    public void TaskWithTaskHostFactory_RuntimeCLR4_ExecutesSuccessfully(string targetFramework)
    {
        var testAsset = CreateTestAssetWithTaskHostFactoryAndRuntime("CLR4", targetFramework);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand.Execute("/t:Build;TestTaskHostFactoryWithRuntime", "/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory with Runtime=CLR4 executed successfully");
    }

    /// <summary>
    /// Creates a test asset with a custom .targets file that declares a task using
    /// TaskHostFactory with a specific Runtime attribute value.
    /// </summary>
    /// <param name="runtime">The runtime value to use (NET, CLR4, CurrentRuntime, etc.)</param>
    /// <param name="targetFramework">The target framework for the test project.</param>
    private TestAsset CreateTestAssetWithTaskHostFactoryAndRuntime(string runtime, string? targetFramework = null)
    {
        targetFramework ??= ToolsetInfo.CurrentTargetFramework;
        var testProject = new TestProject()
        {
            Name = "TaskHostFactoryRuntimeTest",
            TargetFrameworks = targetFramework,
            IsExe = true
        };

        var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: $"{runtime}_{targetFramework}");
        var projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

        // Create a custom .targets file that uses TaskHostFactory with Runtime attribute
        var targetsContent = $"""
            <Project>
              <!-- Declare the task with TaskHostFactory and Runtime attribute -->
              <UsingTask 
                TaskName="Microsoft.NET.Build.Tasks.NETSdkInformation" 
                AssemblyFile="$(MicrosoftNETBuildTasksAssembly)" 
                TaskFactory="TaskHostFactory"
                Runtime="{runtime}" />
            
              <Target Name="TestTaskHostFactoryWithRuntime" AfterTargets="Build">
                <Microsoft.NET.Build.Tasks.NETSdkInformation 
                  FormattedText="TaskHostFactory with Runtime={runtime} executed successfully" />
              </Target>
            </Project>
            """;

        File.WriteAllText(Path.Combine(projectDirectory, "TaskHostFactoryRuntimeTest.targets"), targetsContent);

        // Update the project to import the custom targets file
        var projectPath = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
        var projectXml = XDocument.Load(projectPath);
        var ns = projectXml.Root!.Name.Namespace;
        var importElement = new XElement(ns + "Import",
            new XAttribute("Project", "TaskHostFactoryRuntimeTest.targets"));
        projectXml.Root!.Add(importElement);
        projectXml.Save(projectPath);

        return testAsset;
    }
}
