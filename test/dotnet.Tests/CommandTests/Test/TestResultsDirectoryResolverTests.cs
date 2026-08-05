// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestResultsDirectoryResolverTests
{
    [TestMethod]
    public void ResolveReturnsConfiguredDirectoryForFlatLayout()
    {
        string resultsDirectory = Path.GetFullPath("results");
        TestModule module = CreateModule();
        TestResultsDirectoryResolver resolver = CreateResolver(resultsDirectory, ResultsDirectoryLayout.Flat, module);

        resolver.Resolve(module).Should().Be(resultsDirectory);
    }

    [TestMethod]
    public void ResolveReturnsNullForFlatLayoutWithoutConfiguredDirectory()
    {
        TestModule module = CreateModule();
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.Flat, module);

        resolver.Resolve(module).Should().BeNull();
    }

    [TestMethod]
    public void ResolveCreatesProjectAndPivotDirectoriesUnderConfiguredRoot()
    {
        string resultsDirectory = Path.Combine(WorkingDirectory, "artifacts");
        TestModule module = CreateModule("ProjectA");
        TestResultsDirectoryResolver resolver = CreateResolver(resultsDirectory, ResultsDirectoryLayout.PerModule, module);

        string first = resolver.Resolve(module)!;
        string second = resolver.Resolve(module)!;

        first.Should().Be(second);
        first.Should().Be(Path.Combine(resultsDirectory, "ProjectA", "net10.0_x64"));
    }

    [TestMethod]
    public void ResolveUsesDefaultRootForPerModuleLayout()
    {
        TestModule module = CreateModule("ProjectA");
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.PerModule, module);

        resolver.Resolve(module).Should().Be(Path.Combine(WorkingDirectory, "TestResults", "ProjectA", "net10.0_x64"));
    }

    [TestMethod]
    public void ResolveUsesArtifactsOutputRootAndPerModuleLayoutByDefault()
    {
        string artifactsPath = Path.Combine(WorkingDirectory, "artifacts");
        TestModule module = CreateModule(
            "ProjectA",
            useArtifactsOutput: true,
            artifactsPath: artifactsPath,
            artifactsProjectName: "CustomProject",
            artifactsPivots: "debug_net10.0");
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.Flat, module);

        resolver.Resolve(module).Should().Be(
            Path.Combine(artifactsPath, "test", "CustomProject", "debug_net10.0"));
    }

    [TestMethod]
    public void ResolveKeepsConfiguredResultsDirectoryFlatInArtifactsOutputMode()
    {
        string resultsDirectory = Path.Combine(WorkingDirectory, "custom-results");
        TestModule module = CreateModule(
            "ProjectA",
            useArtifactsOutput: true,
            artifactsPath: Path.Combine(WorkingDirectory, "artifacts"));
        TestResultsDirectoryResolver resolver = CreateResolver(resultsDirectory, ResultsDirectoryLayout.Flat, module);

        resolver.Resolve(module).Should().Be(resultsDirectory);
    }

    [TestMethod]
    public void ResolveHonorsExplicitFlatLayoutInArtifactsOutputMode()
    {
        string artifactsPath = Path.Combine(WorkingDirectory, "artifacts");
        TestModule module = CreateModule("ProjectA", useArtifactsOutput: true, artifactsPath: artifactsPath);
        TestResultsDirectoryResolver resolver = CreateResolver(
            null,
            ResultsDirectoryLayout.Flat,
            layoutSpecified: true,
            module);

        resolver.Resolve(module).Should().Be(Path.Combine(artifactsPath, "test"));
    }

    [TestMethod]
    public void ResolveNestsTargetFrameworksOfTheSameProjectUnderOneProjectDirectory()
    {
        TestModule net10 = CreateModule("ProjectA");
        TestModule net9 = CreateModule("ProjectA") with { TargetFramework = "net9.0" };
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.PerModule, net10, net9);

        string first = resolver.Resolve(net10)!;
        string second = resolver.Resolve(net9)!;

        Path.GetDirectoryName(first).Should().Be(Path.Combine(WorkingDirectory, "TestResults", "ProjectA"));
        Path.GetDirectoryName(first).Should().Be(Path.GetDirectoryName(second));
        Path.GetFileName(first).Should().Be("net10.0_x64");
        Path.GetFileName(second).Should().Be("net9.0_x64");
    }

    [TestMethod]
    public void ResolveKeepsProjectDirectoryCleanWhenProjectNamesAreUnique()
    {
        TestModule projectA = CreateModule("ProjectA");
        TestModule projectB = CreateModule("ProjectB");
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.PerModule, projectA, projectB);

        resolver.Resolve(projectA).Should().Be(Path.Combine(WorkingDirectory, "TestResults", "ProjectA", "net10.0_x64"));
        resolver.Resolve(projectB).Should().Be(Path.Combine(WorkingDirectory, "TestResults", "ProjectB", "net10.0_x64"));
    }

    [TestMethod]
    public void ResolveDisambiguatesDistinctProjectsThatShareAName()
    {
        // Two different 'Tests.csproj' files in one run would otherwise clobber each other.
        TestModule first = CreateModule("Tests", parentDirectory: "src");
        TestModule second = CreateModule("Tests", parentDirectory: "samples");
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.PerModule, first, second);

        string firstPath = resolver.Resolve(first)!;
        string secondPath = resolver.Resolve(second)!;

        firstPath.Should().NotBe(secondPath);
        Path.GetFileName(Path.GetDirectoryName(firstPath)).Should().MatchRegex("^Tests_[0-9a-f]{16}$");
        Path.GetFileName(Path.GetDirectoryName(secondPath)).Should().MatchRegex("^Tests_[0-9a-f]{16}$");
        Path.GetFileName(firstPath).Should().Be("net10.0_x64");
    }

    [TestMethod]
    public void ResolveIsStableAcrossResolverInstancesForTheSameModuleSet()
    {
        TestModule first = CreateModule("Tests", parentDirectory: "src");
        TestModule second = CreateModule("Tests", parentDirectory: "samples");

        string firstRun = CreateResolver(null, ResultsDirectoryLayout.PerModule, first, second).Resolve(first)!;
        string secondRun = CreateResolver(null, ResultsDirectoryLayout.PerModule, first, second).Resolve(first)!;

        firstRun.Should().Be(secondRun);
    }

    [TestMethod]
    public void ResolveKeepsProjectsWithDottedNamesInsideTheResultsRoot()
    {
        // Path.GetFileNameWithoutExtension("...csproj") is "..", which must never be used as a
        // path component or the results would be written outside the results directory.
        TestModule module = CreateModule("..");
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.PerModule, module);

        string actual = resolver.Resolve(module)!;

        string root = Path.Combine(WorkingDirectory, "TestResults");
        actual.Should().StartWith(root + Path.DirectorySeparatorChar);
        Path.GetFileName(Path.GetDirectoryName(actual)).Should().NotBe("..");
    }

    [TestMethod]
    public void ResolveIsIndependentOfTheCurrentDirectory()
    {
        // The same solution must produce the same folder names no matter where dotnet test ran.
        TestModule first = CreateModule("Tests", parentDirectory: "src");
        TestModule second = CreateModule("Tests", parentDirectory: "samples");

        string fromRepoRoot = CreateResolver(null, ResultsDirectoryLayout.PerModule, WorkingDirectory, first, second).Resolve(first)!;
        string fromElsewhere = CreateResolver(null, ResultsDirectoryLayout.PerModule, Path.Combine(WorkingDirectory, "src"), first, second).Resolve(first)!;

        Path.GetFileName(Path.GetDirectoryName(fromRepoRoot))
            .Should().Be(Path.GetFileName(Path.GetDirectoryName(fromElsewhere)));
    }

    [TestMethod]
    public void ResolveUsesRuntimeIdentifierInPivotWhenOneWasRequested()
    {
        TestModule module = CreateModule("ProjectA", runtimeIdentifier: "linux-musl-arm64");
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.PerModule, module);

        Path.GetFileName(resolver.Resolve(module)).Should().Be("net10.0_linux-musl-arm64");
    }

    [TestMethod]
    public void ResolveFallsBackToAssemblyNameWhenModuleHasNoProjectMetadata()
    {
        string targetPath = Path.Combine(WorkingDirectory, "bin", "DirectTests.dll");
        TestModule module = new(
            new RunProperties("dotnet", $"exec \"{targetPath}\"", null),
            ProjectFullPath: null,
            TargetFramework: null,
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: targetPath,
            DotnetRootArchVariableName: null,
            EnvironmentVariables: ImmutableDictionary<string, string>.Empty);
        TestResultsDirectoryResolver resolver = CreateResolver(null, ResultsDirectoryLayout.PerModule, module);

        string actual = resolver.Resolve(module)!;

        Path.GetDirectoryName(actual).Should().Be(Path.Combine(WorkingDirectory, "TestResults", "DirectTests"));
        Path.GetFileName(actual).Should().MatchRegex("^unknown_[a-z0-9]+$");
    }

    private static string WorkingDirectory => Path.GetFullPath("repo");

    private static TestResultsDirectoryResolver CreateResolver(
        string? resultsDirectory,
        ResultsDirectoryLayout layout,
        params TestModule[] modules)
        => CreateResolver(resultsDirectory, layout, WorkingDirectory, layoutSpecified: false, modules);

    private static TestResultsDirectoryResolver CreateResolver(
        string? resultsDirectory,
        ResultsDirectoryLayout layout,
        bool layoutSpecified,
        params TestModule[] modules)
        => CreateResolver(resultsDirectory, layout, WorkingDirectory, layoutSpecified, modules);

    private static TestResultsDirectoryResolver CreateResolver(
        string? resultsDirectory,
        ResultsDirectoryLayout layout,
        string workingDirectory,
        params TestModule[] modules)
        => CreateResolver(resultsDirectory, layout, workingDirectory, layoutSpecified: false, modules);

    private static TestResultsDirectoryResolver CreateResolver(
        string? resultsDirectory,
        ResultsDirectoryLayout layout,
        string workingDirectory,
        bool layoutSpecified,
        params TestModule[] modules)
        => TestResultsDirectoryResolver.Create(
            new PathOptions(
                ProjectOrSolutionPath: null,
                SolutionPath: null,
                TestModules: null,
                ResultsDirectoryPath: resultsDirectory,
                ResultsDirectoryLayout: layout,
                ConfigFilePath: null,
                DiagnosticOutputDirectoryPath: null,
                ResultsDirectoryLayoutSpecified: layoutSpecified),
            modules,
            workingDirectory);

    private static TestModule CreateModule(
        string projectName = "ProjectA",
        string? parentDirectory = null,
        string runtimeIdentifier = "",
        bool useArtifactsOutput = false,
        string? artifactsPath = null,
        string? artifactsProjectName = null,
        string? artifactsPivots = null)
    {
        string projectDirectory = parentDirectory is null
            ? Path.Combine(WorkingDirectory, projectName)
            : Path.Combine(WorkingDirectory, parentDirectory, projectName);
        string targetPath = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "MyTests.dll");

        return new TestModule(
            new RunProperties(
                Command: "dotnet",
                Arguments: targetPath,
                WorkingDirectory: projectDirectory,
                RuntimeIdentifier: runtimeIdentifier,
                DefaultAppHostRuntimeIdentifier: "win-x64",
                TargetFrameworkVersion: "v10.0"),
            ProjectFullPath: Path.Combine(projectDirectory, $"{projectName}.csproj"),
            TargetFramework: "net10.0",
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: targetPath,
            DotnetRootArchVariableName: null,
            EnvironmentVariables: ImmutableDictionary<string, string>.Empty,
            UseArtifactsOutput: useArtifactsOutput,
            ArtifactsPath: artifactsPath,
            ArtifactsProjectName: artifactsProjectName,
            ArtifactsPivots: artifactsPivots);
    }
}
