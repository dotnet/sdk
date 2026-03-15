// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class DotnetupConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalDataDir;

    public DotnetupConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Redirect config path to temp directory
        _originalDataDir = Environment.GetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR");
        Environment.SetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR", _tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR", _originalDataDir);
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
            PathPreference = PathPreference.ShellProfile,
        };

        DotnetupConfig.Write(config);
        DotnetupConfig.Exists().Should().BeTrue();

        var loaded = DotnetupConfig.Read();
        loaded.Should().NotBeNull();
        loaded!.PathPreference.Should().Be(PathPreference.ShellProfile);
        loaded.SchemaVersion.Should().Be("1");
    }

    [Theory]
    [InlineData(PathPreference.DotnetupDotnet)]
    [InlineData(PathPreference.ShellProfile)]
    [InlineData(PathPreference.FullPathReplacement)]
    internal void EnsurePathPreference_ReturnsStoredPreference_WhenConfigExists(PathPreference preference)
    {
        DotnetupConfig.Write(new DotnetupConfigData { PathPreference = preference });

        var result = DotnetupConfig.EnsurePathPreference(interactive: false);

        result.Should().Be(preference);
    }

    [Fact]
    public void EnsurePathPreference_ReturnsNull_WhenNoConfigAndNonInteractive()
    {
        var result = DotnetupConfig.EnsurePathPreference(interactive: false);

        result.Should().BeNull();
    }

    [Fact]
    public void EnsurePathPreference_ReturnsStoredPreference_EvenWhenInteractive()
    {
        // When config already exists, interactive flag doesn't matter — no prompt needed
        DotnetupConfig.Write(new DotnetupConfigData { PathPreference = PathPreference.DotnetupDotnet });

        var result = DotnetupConfig.EnsurePathPreference(interactive: true);

        result.Should().Be(PathPreference.DotnetupDotnet);
    }

    [Fact]
    public void Read_ReturnsNull_WhenNoConfigFile()
    {
        DotnetupConfig.Read().Should().BeNull();
    }

    [Fact]
    public void Read_ReturnsNull_WhenConfigFileIsCorrupt()
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, "not valid json{{{");

        DotnetupConfig.Read().Should().BeNull();
    }
}
