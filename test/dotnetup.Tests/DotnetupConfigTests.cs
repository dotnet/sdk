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
            AccessMode = DotnetAccessMode.Shell,
        };

        DotnetupConfig.Write(config);
        DotnetupConfig.Exists().Should().BeTrue();

        var loaded = DotnetupConfig.Read();
        loaded.Should().NotBeNull();
        loaded!.AccessMode.Should().Be(DotnetAccessMode.Shell);
        loaded.SchemaVersion.Should().Be("1");
    }

    [Theory]
    [InlineData(DotnetAccessMode.None)]
    [InlineData(DotnetAccessMode.Shell)]
    [InlineData(DotnetAccessMode.All)]
    internal void ReadAccessMode_ReturnsStoredPreference_WhenConfigExists(DotnetAccessMode preference)
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = preference });

        var result = DotnetupConfig.ReadAccessMode();

        result.Should().Be(preference);
    }

    [Fact]
    public void ReadAccessMode_ReturnsNull_WhenNoConfig()
    {
        var result = DotnetupConfig.ReadAccessMode();

        result.Should().BeNull();
    }


    [Fact]
    public void Read_ReturnsNull_WhenNoConfigFile()
    {
        DotnetupConfig.Read().Should().BeNull();
    }

    [Theory]
    [InlineData("DotnetupDotnet", DotnetAccessMode.None)]
    [InlineData("ShellProfile", DotnetAccessMode.Shell)]
    [InlineData("FullPathReplacement", DotnetAccessMode.All)]
    internal void Read_LegacyConfig_MapsPropertyNameAndEnumSpelling(string legacyEnumValue, DotnetAccessMode expected)
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
        config!.AccessMode.Should().Be(expected);
        // A missing dotnetupOnPath defaults to true.
        config.DotnetupOnPath.Should().BeTrue();
    }

    [Fact]
    public void WriteAndRead_RoundTripsDotnetupOnPath()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = false });

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
