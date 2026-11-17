// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Pack.Tests;

[TestClass]
[DoNotParallelize]
public class ReleasePropertyProjectLocatorTests : SdkTest
{
    private string? _disablePublishAndPackRelease;

    [TestInitialize]
    public void ClearReleaseDiscoveryEnvironmentVariables()
    {
        _disablePublishAndPackRelease = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE);

        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, null);
    }

    [TestCleanup]
    public void RestoreReleaseDiscoveryEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, _disablePublishAndPackRelease);
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

    private static IReadOnlyDictionary<string, string>? DiscoverPackProperties(string projectOrSolutionPath)
    {
        var locator = new ReleasePropertyProjectLocator(
            userSpecifiedExplicitMSBuildProperties: null,
            propertyToCheck: "PackRelease",
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
