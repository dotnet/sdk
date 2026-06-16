// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class DotnetupConfigTests : IDisposable
{
    private readonly string _tempDir;

    public DotnetupConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Thread-local override — safe for parallel test execution.
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [Fact]
    public void Exists_ReturnsFalse_WhenNoConfigFile()
    {
        DotnetupConfig.Exists().Should().BeFalse();
    }

    [Fact]
    public void WriteAndRead_RoundTrips()
    {
        var config = new DotnetupConfigData
        {
            Env = PathPreference.Shell,
        };

        DotnetupConfig.Write(config);
        DotnetupConfig.Exists().Should().BeTrue();

        var loaded = DotnetupConfig.Read();
        loaded.Should().NotBeNull();
        loaded!.Env.Should().Be(PathPreference.Shell);
        loaded.SchemaVersion.Should().Be("1");
    }

    [Theory]
    [InlineData(PathPreference.None)]
    [InlineData(PathPreference.Shell)]
    [InlineData(PathPreference.All)]
    internal void ReadEnvPreference_ReturnsStoredPreference_WhenConfigExists(PathPreference preference)
    {
        DotnetupConfig.Write(new DotnetupConfigData { Env = preference });

        var result = DotnetupConfig.ReadEnvPreference();

        result.Should().Be(preference);
    }

    [Fact]
    public void ReadEnvPreference_ReturnsNull_WhenNoConfig()
    {
        var result = DotnetupConfig.ReadEnvPreference();

        result.Should().BeNull();
    }


    [Fact]
    public void Read_ReturnsNull_WhenNoConfigFile()
    {
        DotnetupConfig.Read().Should().BeNull();
    }

    [Theory]
    [InlineData("DotnetupDotnet", PathPreference.None)]
    [InlineData("ShellProfile", PathPreference.Shell)]
    [InlineData("FullPathReplacement", PathPreference.All)]
    internal void Read_LegacyConfig_MapsPropertyNameAndEnumSpelling(string legacyEnumValue, PathPreference expected)
    {
        // Simulate a config written by an earlier internal build: the legacy "pathPreference"
        // property name and the legacy enum spellings, and no "dotnetupOnPath" field.
        var legacyJson = $$"""
            {
              "schemaVersion": "1",
              "pathPreference": "{{legacyEnumValue}}"
            }
            """;
        File.WriteAllText(DotnetupPaths.ConfigPath, legacyJson);

        var config = DotnetupConfig.Read();

        config.Should().NotBeNull();
        config!.Env.Should().Be(expected);
        // A missing dotnetupOnPath defaults to true.
        config.DotnetupOnPath.Should().BeTrue();
    }

    [Fact]
    public void WriteAndRead_RoundTripsDotnetupOnPath()
    {
        DotnetupConfig.Write(new DotnetupConfigData { Env = PathPreference.None, DotnetupOnPath = false });

        var loaded = DotnetupConfig.Read();

        loaded!.DotnetupOnPath.Should().BeFalse();
    }

    [Fact]
    public void Read_ReturnsNull_WhenConfigFileIsCorrupt()
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, "not valid json{{{");

        DotnetupConfig.Read().Should().BeNull();
    }
}
