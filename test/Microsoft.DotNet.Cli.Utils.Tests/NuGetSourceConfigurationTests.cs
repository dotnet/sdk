// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.Utils;

[TestClass]
public class NuGetSourceConfigurationTests
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public NuGetSourceConfigurationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_testDirectory, recursive: true);
    }

    [TestMethod]
    public void LoadUsesEnabledConfiguredSources()
    {
        string directory = CreateNuGetConfig(
            """
            <packageSources>
              <add key="enabled" value="enabled-feed" />
              <add key="disabled" value="disabled-feed" />
            </packageSources>
            <disabledPackageSources>
              <add key="disabled" value="true" />
            </disabledPackageSources>
            """);

        NuGetSourceConfiguration configuration = NuGetSourceConfiguration.Load(
            nugetConfig: Path.Combine(directory, "NuGet.config"),
            basePath: directory);

        configuration.PackageSources.Should().ContainSingle();
        configuration.PackageSources[0].Name.Should().Be("enabled");
        configuration.PackageSources[0].Source.Should().Be(Path.Combine(directory, "enabled-feed"));
    }

    [TestMethod]
    public void SourceOverridesAreExclusiveAndAddSourcesAreAdditive()
    {
        string directory = CreateNuGetConfig(
            """
            <packageSources>
              <add key="configured" value="configured-feed" />
            </packageSources>
            """);

        NuGetSourceConfiguration configuration = NuGetSourceConfiguration.Load(
            nugetConfig: Path.Combine(directory, "NuGet.config"),
            sourceFeedOverrides: ["override-feed"],
            additionalSourceFeeds: ["additional-feed", "override-feed"],
            basePath: directory);

        configuration.PackageSources.Select(source => source.Source).Should().Equal(
            Path.Combine(directory, "override-feed"),
            Path.Combine(directory, "additional-feed"));
    }

    [TestMethod]
    public void AddSourcesFollowConfiguredSourcesAndAreDeduplicated()
    {
        string directory = CreateNuGetConfig(
            """
            <packageSources>
              <add key="configured" value="configured-feed" />
            </packageSources>
            """);

        NuGetSourceConfiguration configuration = NuGetSourceConfiguration.Load(
            nugetConfig: Path.Combine(directory, "NuGet.config"),
            additionalSourceFeeds: ["configured-feed", "additional-feed"],
            basePath: directory);

        configuration.PackageSources.Select(source => source.Source).Should().Equal(
            Path.Combine(directory, "configured-feed"),
            Path.Combine(directory, "additional-feed"));
    }

    private string CreateNuGetConfig(string sections)
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "NuGet.config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              {sections}
            </configuration>
            """);
        return _testDirectory;
    }
}
