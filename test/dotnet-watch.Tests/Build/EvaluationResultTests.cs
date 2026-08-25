// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch.UnitTests;

public class EvaluationResultTests
{
    public ProjectGraph CreateGraph(TestDirectory testDir, params (string projectName, string[] targetFrameworks, string[] referencedProjects)[] projects)
    {
        var projectDefinitions = new Dictionary<string, (string[] targetFrameworks, string[] references)>();
        foreach (var (projectName, targetFrameworks, referencedProjects) in projects)
        {
            projectDefinitions[projectName] = (targetFrameworks, referencedProjects);
        }

        var allReferencedProjects = projects
            .SelectMany(p => p.referencedProjects)
            .ToHashSet();

        var entryPoints = projects
            .Where(p => !allReferencedProjects.Contains(p.projectName))
            .Select(p => new ProjectGraphEntryPoint(Path.Combine(testDir.Path, p.projectName)));

        return new ProjectGraph(
            entryPoints,
            ProjectCollection.GlobalProjectCollection,
            (path, globalProperties, collection) =>
            {
                var (targetFrameworks, references) = projectDefinitions[Path.GetFileName(path)];

                var projectXml = ProjectRootElement.Create(collection);
                projectXml.FullPath = Path.Combine(testDir.Path, path);
                projectXml.Sdk = "Microsoft.NET.Sdk";

                var propertyGroup = projectXml.AddPropertyGroup();
                if (targetFrameworks.Length == 1)
                {
                    propertyGroup.AddProperty("TargetFramework", targetFrameworks[0]);
                }
                else if (targetFrameworks.Length > 1)
                {
                    propertyGroup.AddProperty("TargetFrameworks", string.Join(";", targetFrameworks));
                }

                if (references.Length > 0)
                {
                    var itemGroup = projectXml.AddItemGroup();
                    foreach (var reference in references)
                    {
                        itemGroup.AddItem("ProjectReference", reference);
                    }
                }

                return new ProjectInstance(
                    projectXml,
                    globalProperties,
                    toolsVersion: null,
                    subToolsetVersion: null,
                    collection);
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("net9.0")]
    public void CreateDesignTimeBuildRequests_SingleTfm(string? mainTfm)
    {
        var testDir = TestAssetsManager.CreateTestDirectory(identifiers: [mainTfm]);

        var graph = CreateGraph(
            testDir,
            ("main.csproj", ["net9.0"], []));

        var requests = EvaluationResult.CreateDesignTimeBuildRequests(graph, mainProjectTargetFramework: mainTfm, suppressStaticWebAssets: false);

        AssertEx.SequenceEqual(["main (net9.0)"], requests.Select(r => r.ProjectInstance.GetDisplayName()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("net9.0")]
    public void CreateDesignTimeBuildRequests_SingleTfm_WithDependencies(string? mainTfm)
    {
        var testDir = TestAssetsManager.CreateTestDirectory(identifiers: [mainTfm]);

        var graph = CreateGraph(
            testDir,
            ("main.csproj", ["net9.0"], ["dep.csproj"]),
            ("dep.csproj", ["netstandard2.0"], []));

        var requests = EvaluationResult.CreateDesignTimeBuildRequests(graph, mainProjectTargetFramework: mainTfm, suppressStaticWebAssets: false);

        AssertEx.SequenceEqual(
        [
            "dep (netstandard2.0)",
            "main (net9.0)",
        ], requests.Select(r => r.ProjectInstance.GetDisplayName()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("net9.0")]
    public void CreateDesignTimeBuildRequests_SingleTfm_WithMultiTargetedDependencies(string? mainTfm)
    {
        var testDir = TestAssetsManager.CreateTestDirectory(identifiers: [mainTfm]);

        var graph = CreateGraph(
            testDir,
            ("main.csproj", ["net9.0"], ["dep.csproj"]),
            ("dep.csproj", ["netstandard2.0", "net8.0"], []));

        var requests = EvaluationResult.CreateDesignTimeBuildRequests(graph, mainProjectTargetFramework: mainTfm, suppressStaticWebAssets: false);

        AssertEx.SequenceEqual(
        [
            "dep (net8.0)",
            "dep (netstandard2.0)",
            "main (net9.0)",
        ], requests.Select(r => r.ProjectInstance.GetDisplayName()));
    }

    [Fact]
    public void CreateDesignTimeBuildRequests_MultiTfm_WithDependencies_NoMainTfm()
    {
        var testDir = TestAssetsManager.CreateTestDirectory();

        var graph = CreateGraph(
            testDir,
            ("main.csproj", ["net8.0", "net9.0"], ["dep.csproj"]),
            ("dep.csproj", ["netstandard2.0", "net8.0"], []));

        var requests = EvaluationResult.CreateDesignTimeBuildRequests(graph, mainProjectTargetFramework: null, suppressStaticWebAssets: false);

        AssertEx.SequenceEqual(
        [
            "dep (net8.0)",
            "dep (netstandard2.0)",
            "main (net9.0)",
            "main (net8.0)",
        ], requests.Select(r => r.ProjectInstance.GetDisplayName()));
    }

    [Fact]
    public void CreateDesignTimeBuildRequests_MultiTfm_WithDependencies_MainTfm()
    {
        var testDir = TestAssetsManager.CreateTestDirectory();

        var graph = CreateGraph(
            testDir,
            ("main.csproj", ["net8.0", "net9.0"], ["dep.csproj"]),
            ("dep.csproj", ["netstandard2.0", "net8.0", "net9.0"], []));

        var requests = EvaluationResult.CreateDesignTimeBuildRequests(graph, mainProjectTargetFramework: "net8.0", suppressStaticWebAssets: false);

        // main (net9.0) should not be built:
        AssertEx.SequenceEqual(
        [
            "dep (net9.0)",
            "dep (net8.0)",
            "dep (netstandard2.0)",
            "main (net8.0)",
        ], requests.Select(r => r.ProjectInstance.GetDisplayName()));
    }
}
