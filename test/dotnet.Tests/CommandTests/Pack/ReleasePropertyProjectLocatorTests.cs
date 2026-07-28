// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Pack.Tests;

[TestClass]
[DoNotParallelize]
public class ReleasePropertyProjectLocatorTests : SdkTest
{
    private string? _disablePublishAndPackRelease;
    private string? _lazyPublishAndPackReleaseForSolutions;

    [TestInitialize]
    public void ClearReleaseDiscoveryEnvironmentVariables()
    {
        _disablePublishAndPackRelease = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE);
        _lazyPublishAndPackReleaseForSolutions = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS);

        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, null);
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS, null);
    }

    [TestCleanup]
    public void RestoreReleaseDiscoveryEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, _disablePublishAndPackRelease);
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS, _lazyPublishAndPackReleaseForSolutions);
    }

    [TestMethod]
    public void EvaluationContextIsNotRetainedBetweenDiscoveryCalls()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
        string projectPath = Path.Combine(testDirectory, "Test.csproj");
        string propsPath = Path.Combine(testDirectory, "Directory.Build.props");

        File.WriteAllText(projectPath, """
            <Project>
              <Import Project="Directory.Build.props" Condition="Exists('Directory.Build.props')" />
              <PropertyGroup>
                <PackRelease Condition="'$(PackRelease)' == ''">false</PackRelease>
              </PropertyGroup>
            </Project>
            """);
        WritePackRelease(propsPath, value: true);

        var firstResult = DiscoverPackProperties(projectPath);

        Assert.IsNotNull(firstResult);
        Assert.AreEqual("Release", firstResult["Configuration"]);

        File.Delete(propsPath);

        var secondResult = DiscoverPackProperties(projectPath);

        Assert.IsNotNull(secondResult);
        Assert.IsEmpty(secondResult);
    }

    [TestMethod]
    public void SharedContextKeepsSameNamedImportedFilesSeparate()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
        string projectA = CreateProjectWithImport(testDirectory, "A", "Release.props");
        string projectB = CreateProjectWithImport(testDirectory, "B", "Release.props");

        WritePackRelease(Path.Combine(testDirectory, "A", "Release.props"), value: true);
        WritePackRelease(Path.Combine(testDirectory, "B", "Release.props"), value: false);

        string solutionPath = CreateSolution(testDirectory, projectA, projectB);

        var exception = Assert.ThrowsExactly<GracefulException>(() => DiscoverPackProperties(solutionPath));

        exception.Message.Should().Contain("NETSDK1197");
    }

    [TestMethod]
    public void SharedContextKeepsMissingImportsProjectSpecific()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
        string projectA = CreateProjectWithConditionalImport(testDirectory, "A");
        string projectB = CreateProjectWithConditionalImport(testDirectory, "B");

        WritePackRelease(Path.Combine(testDirectory, "A", "Release.props"), value: true);

        string solutionPath = CreateSolution(testDirectory, projectA, projectB);

        var exception = Assert.ThrowsExactly<GracefulException>(() => DiscoverPackProperties(solutionPath));

        exception.Message.Should().Contain("NETSDK1197");
    }

    private static IReadOnlyDictionary<string, string>? DiscoverPackProperties(string projectOrSolutionPath)
    {
        var locator = new ReleasePropertyProjectLocator(
            userSpecifiedExplicitMSBuildProperties: null,
            propertyToCheck: "PackRelease",
            commandOptions: new ReleasePropertyProjectLocator.DependentCommandOptions([projectOrSolutionPath]));

        return locator.GetCustomDefaultConfigurationValueIfSpecified();
    }

    private static string CreateProjectWithImport(string root, string projectName, string import)
    {
        string projectDirectory = Directory.CreateDirectory(Path.Combine(root, projectName)).FullName;
        string projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");
        File.WriteAllText(projectPath, $"""
            <Project>
              <Import Project="{import}" />
            </Project>
            """);
        return projectPath;
    }

    private static string CreateProjectWithConditionalImport(string root, string projectName)
    {
        string projectDirectory = Directory.CreateDirectory(Path.Combine(root, projectName)).FullName;
        string projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");
        File.WriteAllText(projectPath, """
            <Project>
              <Import Project="Release.props" Condition="Exists('Release.props')" />
              <PropertyGroup>
                <PackRelease Condition="'$(PackRelease)' == ''">false</PackRelease>
              </PropertyGroup>
            </Project>
            """);
        return projectPath;
    }

    private static string CreateSolution(string root, params string[] projectPaths)
    {
        string projects = string.Join(
            Environment.NewLine,
            projectPaths.Select(path => $"  <Project Path=\"{Path.GetRelativePath(root, path).Replace('\\', '/')}\" />"));
        string solutionPath = Path.Combine(root, "Test.slnx");
        File.WriteAllText(solutionPath, $"""
            <Solution>
            {projects}
            </Solution>
            """);
        return solutionPath;
    }

    private static void WritePackRelease(string path, bool value)
    {
        File.WriteAllText(path, $"""
            <Project>
              <PropertyGroup>
                <PackRelease>{value.ToString().ToLowerInvariant()}</PackRelease>
              </PropertyGroup>
            </Project>
            """);
    }
}
