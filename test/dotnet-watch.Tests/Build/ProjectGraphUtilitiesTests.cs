// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch.UnitTests.Build;

[TestClass]
public class ProjectGraphUtilitiesTests
{
    public TestContext TestContext { get; set; } = null!;
    private DualOutputHelper Output => field ??= new(new TestContextOutputHelper(TestContext));
    private TestAssetsManager TestAssets => field ??= new(Output);

    [TestMethod]
    [DataRow(@"a\b/c", "a/b/c", "a/b")]
    [DataRow(@"a\b/c/", "a/b/c/", "a/b/c")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void NormalizeSeparatorsWhenReadingOutputDirectoryInPosixSystems(string input, string expectedIntermediatePath, string expectedOutputPath)
    {
        var project = new TestProject("A");
        var testAsset = TestAssets.CreateTestProject(project);
        var projectPath = Path.Combine(testAsset.TestRoot, "A", "A.csproj");

        var projectInstance = new ProjectInstance(
            projectFile: projectPath,
            globalProperties: new Dictionary<string, string>
            {
                [PropertyNames.IntermediateOutputPath] = input,
                [PropertyNames.TargetPath] = input
            },
            toolsVersion: "Current"
        );
        Assert.AreEqual(Path.Combine(testAsset.TestRoot, "A", expectedIntermediatePath), projectInstance.GetIntermediateOutputDirectory());
        Assert.AreEqual(Path.Combine(testAsset.TestRoot, "A", expectedOutputPath), projectInstance.GetOutputDirectory());
    }

    [TestMethod]
    [DataRow(@"a\b/c", @"a\b/c", @"a\b")]
    [DataRow(@"a\b/c/", @"a\b/c/", @"a\b/c")]
    [OSCondition(OperatingSystems.Windows)]
    public void ShouldNotNormalizeSeparatorsInWindows(string input, string expectedIntermediatePath, string expectedOutputPath)
    {
        var project = new TestProject("A");
        var testAsset = TestAssets.CreateTestProject(project);
        var projectPath = Path.Combine(testAsset.TestRoot, "A", "A.csproj");

        var projectInstance = new ProjectInstance(
            projectFile: projectPath,
            globalProperties: new Dictionary<string, string>
            {
                [PropertyNames.IntermediateOutputPath] = input,
                [PropertyNames.TargetPath] = input
            },
            toolsVersion: "Current"
        );
        Assert.AreEqual(Path.Combine(testAsset.TestRoot, "A", expectedIntermediatePath), projectInstance.GetIntermediateOutputDirectory());
        Assert.AreEqual(Path.Combine(testAsset.TestRoot, "A", expectedOutputPath), projectInstance.GetOutputDirectory());
    }

    [TestMethod]
    [DataRow(
        @"bin\Debug//**;bin\/**;bin\Debug/linux-x64/publish//**;**/*.user",
        new string[] {
        @"bin\Debug//**",
        @"bin\/**",
        @"bin\Debug/linux-x64/publish//**",
        @"**/*.user",
    })]
    public void DefaultItemExcludesDontNeedNormalization(string input, string[] expected)
    {

        var project = new TestProject("A");
        var testAsset = TestAssets.CreateTestProject(project);
        var projectPath = Path.Combine(testAsset.TestRoot, "A", "A.csproj");

        var graph = new ProjectGraph(projectPath, new Dictionary<string, string> { [PropertyNames.DefaultItemExcludes] = input });
        Assert.AreSequenceEqual(expected, graph.ProjectNodes.First().GetDefaultItemExcludes());
    }
}
