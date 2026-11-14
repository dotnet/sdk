// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Pack.Tests;

[TestClass]
[DoNotParallelize]
public class ReleasePropertyProjectLocatorTests : SdkTest
{
    private string? _disablePublishAndPackRelease;
    private string? _lazyPublishAndPackRelease;

    [TestInitialize]
    public void ClearReleaseDiscoveryEnvironmentVariables()
    {
        _disablePublishAndPackRelease = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE);
        _lazyPublishAndPackRelease = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS);

        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, null);
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS, null);
    }

    [TestCleanup]
    public void RestoreReleaseDiscoveryEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, _disablePublishAndPackRelease);
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS, _lazyPublishAndPackRelease);
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

        var firstResult = DiscoverReleaseProperties(projectPath, "PackRelease");

        Assert.IsNotNull(firstResult);
        Assert.AreEqual("Release", firstResult["Configuration"]);

        File.Delete(propsPath);

        var secondResult = DiscoverReleaseProperties(projectPath, "PackRelease");

        Assert.IsNotNull(secondResult);
        Assert.IsEmpty(secondResult);
    }

    [TestMethod]
    [DataRow(".sln", false, "PackRelease")]
    [DataRow(".sln", true, "PackRelease")]
    [DataRow(".slnx", false, "PackRelease")]
    [DataRow(".slnx", true, "PackRelease")]
    [DataRow(".sln", false, "PublishRelease")]
    [DataRow(".sln", true, "PublishRelease")]
    [DataRow(".slnx", false, "PublishRelease")]
    [DataRow(".slnx", true, "PublishRelease")]
    public void SolutionDiscoverySkipsSharedProjects(string solutionExtension, bool lazy, string propertyName)
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory(identifier: $"{solutionExtension}-{lazy}-{propertyName}").Path;
        var solutionPath = Path.Combine(testDirectory, $"Test{solutionExtension}");
        File.WriteAllText(Path.Combine(testDirectory, "A.Shared.shproj"), $"""
            <Project>
              <PropertyGroup>
                <{propertyName}>false</{propertyName}>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(testDirectory, "Z.App.csproj"), $"""
            <Project>
              <PropertyGroup>
                <{propertyName}>true</{propertyName}>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(solutionPath, solutionExtension == ".sln"
            ? """
              Microsoft Visual Studio Solution File, Format Version 12.00
              Project("{D954291E-2A0B-460D-934E-DC6B0785DB48}") = "Shared", "A.Shared.shproj", "{11111111-1111-1111-1111-111111111111}"
              EndProject
              Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "App", "Z.App.csproj", "{22222222-2222-2222-2222-222222222222}"
              EndProject
              Global
              EndGlobal
              """
            : """
              <Solution>
                <Project Path="A.Shared.shproj" />
                <Project Path="Z.App.csproj" />
              </Solution>
              """);
        Environment.SetEnvironmentVariable(
            EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS,
            lazy ? "true" : null);

        var result = DiscoverReleaseProperties(solutionPath, propertyName);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TryGetValue("Configuration", out var configuration));
        Assert.AreEqual("Release", configuration);
        Assert.AreEqual("true", result[$"_SolutionLevel{propertyName}"]);
    }

    private static IReadOnlyDictionary<string, string>? DiscoverReleaseProperties(string projectOrSolutionPath, string propertyName)
    {
        var locator = new ReleasePropertyProjectLocator(
            userSpecifiedExplicitMSBuildProperties: null,
            propertyToCheck: propertyName,
            commandOptions: new ReleasePropertyProjectLocator.DependentCommandOptions([projectOrSolutionPath]));

        return locator.GetCustomDefaultConfigurationValueIfSpecified();
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
