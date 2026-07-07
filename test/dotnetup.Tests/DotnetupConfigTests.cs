// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
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

    [TestMethod]
    public void Exists_ReturnsFalse_WhenNoConfigFile()
    {
        DotnetupConfig.Exists().Should().BeFalse();
    }

    [TestMethod]
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

    [TestMethod]
    [DataRow(DotnetAccessMode.None)]
    [DataRow(DotnetAccessMode.Shell)]
    [DataRow(DotnetAccessMode.Full)]
    internal void ReadAccessMode_ReturnsStoredPreference_WhenConfigExists(DotnetAccessMode accessMode)
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = accessMode });

        var result = DotnetupConfig.ReadAccessMode();

        result.Should().Be(accessMode);
    }

    [TestMethod]
    public void ReadAccessMode_ReturnsNull_WhenNoConfig()
    {
        var result = DotnetupConfig.ReadAccessMode();

        result.Should().BeNull();
    }


    [TestMethod]
    public void Read_ReturnsNull_WhenNoConfigFile()
    {
        DotnetupConfig.Read().Should().BeNull();
    }

    [TestMethod]
    public void Read_ReturnsNull_WhenConfigFileIsCorrupt()
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, "not valid json{{{");

        DotnetupConfig.Read().Should().BeNull();
    }

    [TestMethod]
    [DataRow("DotnetupDotnet", DotnetAccessMode.None)]
    [DataRow("ShellProfile", DotnetAccessMode.Shell)]
    [DataRow("FullPathReplacement", DotnetAccessMode.Full)]
    internal void Read_LegacyConfig_MapsPropertyNameAndEnumSpelling(string legacyEnumValue, DotnetAccessMode expected)
    {
        DotnetupPaths.EnsureDataDirectoryExists();
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
        config.DotnetupOnPath.Should().BeTrue();
    }

    [TestMethod]
    public void WriteAndRead_RoundTripsDotnetupOnPath()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = false });

        var loaded = DotnetupConfig.Read();

        loaded!.DotnetupOnPath.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(1, DotnetAccessMode.None)]
    [DataRow(2, DotnetAccessMode.Shell)]
    [DataRow(3, DotnetAccessMode.Full)]
    internal void Read_LegacyNumericAccessMode_Maps(int numeric, DotnetAccessMode expected)
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, $$"""{ "schemaVersion": "1", "accessMode": {{numeric}} }""");

        DotnetupConfig.Read()!.AccessMode.Should().Be(expected);
    }

    [TestMethod]
    public void Read_OutOfRangeNumericAccessMode_TreatedAsCorrupt()
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, """{ "schemaVersion": "1", "accessMode": 99 }""");

        DotnetupConfig.Read().Should().BeNull();
    }
}
