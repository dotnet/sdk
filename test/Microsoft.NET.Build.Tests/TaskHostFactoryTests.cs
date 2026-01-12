// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;

namespace Microsoft.NET.Build.Tests;

/// <summary>
/// Tests to ensure MSBuild tasks declared with TaskHostFactory work correctly.
/// TaskHostFactory enables tasks to run out-of-process, which is important for
/// cross-runtime scenarios (e.g., .NET Core tasks from .NET Framework MSBuild).
/// 
/// This guards against regression of https://github.com/dotnet/sdk/issues/12751.
/// </summary>
public class TaskHostFactoryTests(ITestOutputHelper log) : SdkTest(log)
{
    // Supported .NET versions for TaskHostFactory tests
    // These are the currently supported LTS and STS versions
    public static readonly string[] SupportedTargetFrameworks = ["net8.0", "net9.0", ToolsetInfo.CurrentTargetFramework];

    /// <summary>
    /// Verifies that a custom task declared with TaskHostFactory executes successfully
    /// when building with the dotnet CLI across all supported target frameworks.
    /// </summary>
    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    public void TaskWithTaskHostFactory_ExecutesSuccessfully_WithDotnetBuild(string targetFramework)
    {
        var testAsset = CreateTestAssetWithTaskHostFactoryTarget(targetFramework);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand.Execute("/t:Build;TestTaskHostFactory", "/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory: NETSdkInformation task executed successfully");
    }

    /// <summary>
    /// Verifies that a custom task declared with TaskHostFactory executes successfully
    /// when building with Full MSBuild (Visual Studio) across all supported target frameworks.
    /// This is particularly important as TaskHostFactory is designed to handle cross-runtime 
    /// scenarios where .NET Core tasks need to run from .NET Framework MSBuild.
    /// </summary>
    [FullMSBuildOnlyTheory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    public void TaskWithTaskHostFactory_ExecutesSuccessfully_WithFullMSBuild(string targetFramework)
    {
        var testAsset = CreateTestAssetWithTaskHostFactoryTarget(targetFramework);

        var buildCommand = new MSBuildCommand(Log, "Build;TestTaskHostFactory", Path.Combine(testAsset.TestRoot, "TaskHostFactoryTest"));
        buildCommand.Execute("/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory: NETSdkInformation task executed successfully");
    }

    /// <summary>
    /// Verifies that building a standard .NET project works correctly across all supported
    /// target frameworks, which implicitly uses SDK tasks. The SDK declares many tasks with 
    /// TaskHostFactory for cross-runtime compatibility.
    /// </summary>
    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    public void SdkBuild_WithImplicitTaskHostFactoryTasks_Succeeds(string targetFramework)
    {
        var testProject = new TestProject()
        {
            Name = "TaskHostFactoryBuildTest",
            TargetFrameworks = targetFramework,
            IsExe = true
        };

        var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand.Execute()
            .Should()
            .Pass()
            .And
            .NotHaveStdOutContaining("MSB4018") // Task execution error
            .And
            .NotHaveStdOutContaining("MSB4062"); // Could not load task
    }

    /// <summary>
    /// Verifies that a task using TaskHostFactory can be executed multiple times
    /// in the same build. This tests the stability of the out-of-process task host
    /// and ensures it properly handles repeated invocations.
    /// </summary>
    [Fact]
    public void TaskWithTaskHostFactory_ExecutesMultipleTimes_Successfully()
    {
        var testAsset = CreateTestAssetWithMultipleTaskHostFactoryCalls();

        var buildCommand = new BuildCommand(testAsset);
        buildCommand.Execute("/t:Build;TestMultipleTaskHostFactoryCalls", "/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory call 1 completed")
            .And
            .HaveStdOutContaining("TaskHostFactory call 2 completed")
            .And
            .HaveStdOutContaining("TaskHostFactory call 3 completed");
    }

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
        buildCommand.Execute("/t:Build;TestTaskHostFactoryWithRuntime", "/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory with Runtime=NET executed successfully");
    }

    /// <summary>
    /// Verifies that TaskHostFactory with Runtime="NET" works correctly with Full MSBuild
    /// across all supported target frameworks. This tests the cross-runtime scenario where
    /// Full MSBuild (.NET Framework) runs a task on the .NET runtime.
    /// </summary>
    [FullMSBuildOnlyTheory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    public void TaskWithTaskHostFactory_RuntimeNET_WorksWithFullMSBuild(string targetFramework)
    {
        var testAsset = CreateTestAssetWithTaskHostFactoryAndRuntime("NET", targetFramework);

        var buildCommand = new MSBuildCommand(Log, "Build;TestTaskHostFactoryWithRuntime", Path.Combine(testAsset.TestRoot, "TaskHostFactoryRuntimeTest"));
        buildCommand.Execute("/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory with Runtime=NET executed successfully");
    }

    /// <summary>
    /// Verifies that TaskHostFactory with Runtime="CLR4" attribute executes successfully.
    /// CLR4 specifies that the task should run on .NET Framework 4.x CLR.
    /// This test only runs with Full MSBuild as CLR4 requires .NET Framework.
    /// </summary>
    [FullMSBuildOnlyFact]
    public void TaskWithTaskHostFactory_RuntimeCLR4_ExecutesSuccessfully()
    {
        var testAsset = CreateTestAssetWithTaskHostFactoryAndRuntime("CLR4", ToolsetInfo.CurrentTargetFramework);

        var buildCommand = new MSBuildCommand(Log, "Build;TestTaskHostFactoryWithRuntime", Path.Combine(testAsset.TestRoot, "TaskHostFactoryRuntimeTest"));
        buildCommand.Execute("/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining("TaskHostFactory with Runtime=CLR4 executed successfully");
    }

    /// <summary>
    /// Verifies that TaskHostFactory works with different Runtime values using theory.
    /// Tests multiple runtime configurations to ensure broad compatibility.
    /// </summary>
    /// <param name="runtime">The runtime value to test (NET, CLR4, etc.)</param>
    [Theory]
    [InlineData("NET")]
    [InlineData("CurrentRuntime")]
    public void TaskWithTaskHostFactory_VariousRuntimes_ExecuteSuccessfully(string runtime)
    {
        var testAsset = CreateTestAssetWithTaskHostFactoryAndRuntime(runtime);

        var buildCommand = new BuildCommand(testAsset);
        buildCommand.Execute("/t:Build;TestTaskHostFactoryWithRuntime", "/v:n")
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining($"TaskHostFactory with Runtime={runtime} executed successfully");
    }

        /// <summary>
    /// Creates a test asset with a custom .targets file that declares a task using
    /// TaskHostFactory and provides a target to invoke it.
    /// </summary>
    private TestAsset CreateTestAssetWithTaskHostFactoryTarget(string? targetFramework = null)
    {
        targetFramework ??= ToolsetInfo.CurrentTargetFramework;
        
        var testProject = new TestProject()
        {
            Name = "TaskHostFactoryTest",
            TargetFrameworks = targetFramework,
            IsExe = true
        };

        var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
        var projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

        // Create a custom .targets file that uses TaskHostFactory
        // We use NETSdkInformation task from Microsoft.NET.Build.Tasks.dll since it's
        // available in the SDK and demonstrates the TaskHostFactory mechanism
        var targetsContent = """
            <Project>
              <!-- Declare the task with TaskHostFactory to enable out-of-process execution -->
              <UsingTask 
                TaskName="Microsoft.NET.Build.Tasks.NETSdkInformation" 
                AssemblyFile="$(MicrosoftNETBuildTasksAssembly)" 
                TaskFactory="TaskHostFactory" />
            
              <Target Name="TestTaskHostFactory" AfterTargets="Build">
                <Microsoft.NET.Build.Tasks.NETSdkInformation 
                  ResourceName="TaskHostFactory: NETSdkInformation task executed successfully"
                  FormatArguments="" />
              </Target>
            </Project>
            """;

        File.WriteAllText(Path.Combine(projectDirectory, "TaskHostFactoryTest.targets"), targetsContent);

        // Update the project to import the custom targets file
        var projectPath = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
        var projectXml = XDocument.Load(projectPath);
        var importElement = new XElement(XName.Get("Import", "http://schemas.microsoft.com/developer/msbuild/2003"),
            new XAttribute("Project", "TaskHostFactoryTest.targets"));
        projectXml.Root!.Add(importElement);
        projectXml.Save(projectPath);

        return testAsset;
    }

    /// <summary>
    /// Creates a test asset with a custom .targets file that invokes a TaskHostFactory
    /// task multiple times to test stability.
    /// </summary>
    private TestAsset CreateTestAssetWithMultipleTaskHostFactoryCalls()
    {
        var testProject = new TestProject()
        {
            Name = "MultipleTaskHostFactoryCalls",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true
        };

        var testAsset = _testAssetsManager.CreateTestProject(testProject);
        var projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

        // Create a custom .targets file that calls the task multiple times
        var targetsContent = """
            <Project>
              <!-- Declare the task with TaskHostFactory -->
              <UsingTask 
                TaskName="Microsoft.NET.Build.Tasks.NETSdkInformation" 
                AssemblyFile="$(MicrosoftNETBuildTasksAssembly)" 
                TaskFactory="TaskHostFactory" />
            
              <Target Name="TestMultipleTaskHostFactoryCalls" AfterTargets="Build">
                <Microsoft.NET.Build.Tasks.NETSdkInformation 
                  ResourceName="TaskHostFactory call 1 completed"
                  FormatArguments="" />
                <Microsoft.NET.Build.Tasks.NETSdkInformation 
                  ResourceName="TaskHostFactory call 2 completed"
                  FormatArguments="" />
                <Microsoft.NET.Build.Tasks.NETSdkInformation 
                  ResourceName="TaskHostFactory call 3 completed"
                  FormatArguments="" />
              </Target>
            </Project>
            """;

        File.WriteAllText(Path.Combine(projectDirectory, "MultipleTaskHostFactoryCalls.targets"), targetsContent);

        // Update the project to import the custom targets file
        var projectPath = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
        var projectXml = XDocument.Load(projectPath);
        var importElement = new XElement(XName.Get("Import", "http://schemas.microsoft.com/developer/msbuild/2003"),
            new XAttribute("Project", "MultipleTaskHostFactoryCalls.targets"));
        projectXml.Root!.Add(importElement);
        projectXml.Save(projectPath);

        return testAsset;
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
                  ResourceName="TaskHostFactory with Runtime={runtime} executed successfully"
                  FormatArguments="" />
              </Target>
            </Project>
            """;

        File.WriteAllText(Path.Combine(projectDirectory, "TaskHostFactoryRuntimeTest.targets"), targetsContent);

        // Update the project to import the custom targets file
        var projectPath = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
        var projectXml = XDocument.Load(projectPath);
        var importElement = new XElement(XName.Get("Import", "http://schemas.microsoft.com/developer/msbuild/2003"),
            new XAttribute("Project", "TaskHostFactoryRuntimeTest.targets"));
        projectXml.Root!.Add(importElement);
        projectXml.Save(projectPath);

        return testAsset;
    }
}
