// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.UnitTests;

[TestClass]
public class GivenARunReadyToRunCompilerMultiThreading : SdkTest
{
    [TestMethod]
    public void RelativePathsResolveIndependentlyForEachTask()
    {
        string firstProjectDirectory = TestAssetsManager.CreateTestDirectory(identifier: "first").Path;
        string secondProjectDirectory = TestAssetsManager.CreateTestDirectory(identifier: "second").Path;
        string relativeToolPath = Path.Combine("tools", "crossgen2");
        string relativeInputPath = Path.Combine("input", "app.dll");

        CreateFile(Path.Combine(firstProjectDirectory, relativeToolPath));
        CreateFile(Path.Combine(firstProjectDirectory, relativeInputPath));
        CreateFile(Path.Combine(secondProjectDirectory, relativeToolPath));
        CreateFile(Path.Combine(secondProjectDirectory, relativeInputPath));

        var firstTask = CreateTask(firstProjectDirectory, relativeToolPath, relativeInputPath);
        var secondTask = CreateTask(secondProjectDirectory, relativeToolPath, relativeInputPath);

        firstTask.ValidateParametersForTest().Should().BeTrue();
        secondTask.ValidateParametersForTest().Should().BeTrue();
        firstTask.GenerateFullPathToToolForTest().Should().Be(Path.Combine(firstProjectDirectory, relativeToolPath));
        secondTask.GenerateFullPathToToolForTest().Should().Be(Path.Combine(secondProjectDirectory, relativeToolPath));
    }

    [TestMethod]
    public void EmptyToolPathUsesExistingValidationDiagnostic()
    {
        string projectDirectory = TestAssetsManager.CreateTestDirectory().Path;
        var task = CreateTask(projectDirectory, string.Empty, Path.Combine("input", "app.dll"));

        task.ValidateParametersForTest().Should().BeFalse();
        ((MockBuildEngine)task.BuildEngine).Errors.Should().ContainSingle();
    }

    private static TestableRunReadyToRunCompiler CreateTask(
        string projectDirectory,
        string relativeToolPath,
        string relativeInputPath)
    {
        var crossgen2Tool = new TaskItem(relativeToolPath);
        crossgen2Tool.SetMetadata(MetadataKeys.TargetOS, "windows");
        crossgen2Tool.SetMetadata(MetadataKeys.TargetArch, "x64");

        var compilationEntry = new TaskItem(relativeInputPath);
        compilationEntry.SetMetadata(MetadataKeys.OutputR2RImage, Path.Combine("output", "app.dll"));

        return new TestableRunReadyToRunCompiler
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDirectory),
            Crossgen2Tool = crossgen2Tool,
            CompilationEntry = compilationEntry,
            ImplementationAssemblyReferences = [],
            UseCrossgen2 = true,
        };
    }

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
    }

    private sealed class TestableRunReadyToRunCompiler : RunReadyToRunCompiler
    {
        public bool ValidateParametersForTest() => ValidateParameters();

        public string GenerateFullPathToToolForTest() => GenerateFullPathToTool();
    }
}