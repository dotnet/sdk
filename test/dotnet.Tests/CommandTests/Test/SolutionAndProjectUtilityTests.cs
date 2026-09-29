// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class SolutionAndProjectUtilityTests : SdkTest
{
    public static IEnumerable<(string[] files, string expectedFile, bool isSolution)> UnambiguousProjectOrSolutionCases =>
    [
        (["App.csproj"], "App.csproj", false),
        (["App.sln"], "App.sln", true),
        (["App.slnx"], "App.slnx", true),
        (["App.slnf"], "App.slnf", true),
        (["App.csproj", "App.sln"], "App.sln", true),
        (["App.csproj", "App.slnf"], "App.csproj", false),
        (["App.csproj", "build.proj"], "build.proj", false),
    ];

    public static IEnumerable<(string[] files, bool messageIncludesDirectory)> AmbiguousProjectOrSolutionCases =>
    [
        (["App.csproj", "Other.csproj"], true),
        (["App.csproj", "Other.vbproj"], true),
        (["App.sln", "Other.slnx"], true),
        (["App.csproj", "Other.sln"], false),
        (["First.slnf", "Second.slnf"], false),
    ];

    [TestMethod]
    [DynamicData(nameof(UnambiguousProjectOrSolutionCases))]
    public void TryGetProjectOrSolutionFilePath_WithUnambiguousFiles_SelectsExpectedFile(
        string[] files,
        string expectedFile,
        bool isSolution)
    {
        string directory = CreateDirectoryWithFiles(files);

        var result = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(
            directory,
            out string selectedFile,
            out bool selectedIsSolution);

        result.SolutionOrProjectFileFound.Should().BeTrue();
        result.Message.Should().BeEmpty();
        selectedFile.Should().Be(Path.Combine(directory, expectedFile));
        selectedIsSolution.Should().Be(isSolution);
    }

    [TestMethod]
    [DynamicData(nameof(AmbiguousProjectOrSolutionCases))]
    public void TryGetProjectOrSolutionFilePath_WithAmbiguousFiles_ReturnsError(
        string[] files,
        bool messageIncludesDirectory)
    {
        string directory = CreateDirectoryWithFiles(files);

        var result = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(
            directory,
            out _,
            out _);

        result.SolutionOrProjectFileFound.Should().BeFalse();
        result.Message.Should().NotBeEmpty();
        result.Message.Contains(directory).Should().Be(messageIncludesDirectory);
    }

    [TestMethod]
    public void TryGetProjectOrSolutionFilePath_WithNoFiles_ReturnsError()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;

        var result = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(
            directory,
            out string selectedFile,
            out bool isSolution);

        result.SolutionOrProjectFileFound.Should().BeFalse();
        result.Message.Should().NotBeEmpty();
        selectedFile.Should().BeEmpty();
        isSolution.Should().BeFalse();
    }

    [TestMethod]
    public void TryGetProjectOrSolutionFilePath_WithMissingDirectory_ReturnsError()
    {
        string directory = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "missing");

        var result = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(
            directory,
            out string selectedFile,
            out bool isSolution);

        result.SolutionOrProjectFileFound.Should().BeFalse();
        result.Message.Should().Contain(directory);
        selectedFile.Should().BeEmpty();
        isSolution.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("App.sln")]
    [DataRow("App.slnx")]
    [DataRow("App.slnf")]
    public void TryGetSolutionFilePath_WithSingleSolution_SelectsExpectedFile(string file)
    {
        string directory = CreateDirectoryWithFiles(file);

        var result = SolutionAndProjectUtility.TryGetSolutionFilePath(directory, out string selectedFile);

        result.SolutionFileFound.Should().BeTrue();
        result.Message.Should().BeEmpty();
        selectedFile.Should().Be(Path.Combine(directory, file));
    }

    [TestMethod]
    public void TryGetSolutionFilePath_WithNoSolution_ReturnsError()
    {
        string directory = CreateDirectoryWithFiles("App.csproj");

        var result = SolutionAndProjectUtility.TryGetSolutionFilePath(directory, out string selectedFile);

        result.SolutionFileFound.Should().BeFalse();
        result.Message.Should().Contain(directory);
        selectedFile.Should().BeEmpty();
    }

    [TestMethod]
    public void TryGetSolutionFilePath_WithMultipleSolutions_ReturnsError()
    {
        string directory = CreateDirectoryWithFiles("App.sln", "Other.slnx", "Filtered.slnf");

        var result = SolutionAndProjectUtility.TryGetSolutionFilePath(directory, out string selectedFile);

        result.SolutionFileFound.Should().BeFalse();
        result.Message.Should().Contain(directory);
        selectedFile.Should().BeEmpty();
    }

    [TestMethod]
    public void TryGetSolutionFilePath_WithMissingDirectory_ReturnsError()
    {
        string directory = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "missing");

        var result = SolutionAndProjectUtility.TryGetSolutionFilePath(directory, out string selectedFile);

        result.SolutionFileFound.Should().BeFalse();
        result.Message.Should().Contain(directory);
        selectedFile.Should().BeEmpty();
    }

    [TestMethod]
    public void ValidateBuildPathOptionsAcceptsUppercaseSolutionExtension()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        string solution = Path.Combine(directory, "Tests.SLNX");
        File.WriteAllText(solution, "<Solution />");
        var pathOptions = new PathOptions(
            ProjectOrSolutionPath: null,
            SolutionPath: solution,
            TestModules: null,
            ResultsDirectoryPath: null,
            ResultsDirectoryLayout.Flat,
            ConfigFilePath: null,
            DiagnosticOutputDirectoryPath: null);

        bool valid = ValidationUtility.ValidateBuildPathOptions(
            pathOptions,
            out string? selectedPath,
            out bool isSolution);

        valid.Should().BeTrue();
        isSolution.Should().BeTrue();
        selectedPath.Should().Be(solution);
    }

    [TestMethod]
    public void TraversalProjectVisitSetUsesFileSystemCaseSensitivity()
    {
        HashSet<string> visited = SolutionAndProjectUtility.CreateTraversalProjectVisitSet();

        visited.Add(Path.Combine("repo", "Tests", "Project.csproj")).Should().BeTrue();
        bool addedDifferentCase = visited.Add(Path.Combine("repo", "tests", "project.csproj"));

        addedDifferentCase.Should().Be(FileUtilities.GetIsFileSystemCaseSensitive());
    }

    private string CreateDirectoryWithFiles(params string[] files)
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        foreach (string file in files)
        {
            File.WriteAllText(Path.Combine(directory, file), string.Empty);
        }

        return directory;
    }
}
