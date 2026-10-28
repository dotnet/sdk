// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
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
    public void Write_SerializesAccessModeAsLowercaseString()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Everywhere });

        var text = File.ReadAllText(DotnetupPaths.ConfigPath);

        text.Should().Contain("\"accessMode\": \"everywhere\"");
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.None)]
    [DataRow(DotnetAccessMode.Shell)]
    [DataRow(DotnetAccessMode.Everywhere)]
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
    [DataRow("ShellProfile")]
    [DataRow("FullPathReplacement")]
    [DataRow("full")]
    internal void Read_LegacyEnumSpelling_UnderAccessMode_TreatedAsCorrupt(string legacyEnumValue)
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        var json = $$"""
            {
              "schemaVersion": "1",
              "accessMode": "{{legacyEnumValue}}"
            }
            """;
        File.WriteAllText(DotnetupPaths.ConfigPath, json);

        // The pre-rename enum spellings are no longer accepted; an unrecognized value is treated
        // as a corrupt config rather than silently mapped.
        DotnetupConfig.Read().Should().BeNull();
    }

    [TestMethod]
    public void Read_ConfigWithoutAccessMode_IsTreatedAsNotSetUp()
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        var legacyJson = """
            {
              "schemaVersion": "1",
              "pathPreference": "ShellProfile"
            }
            """;
        File.WriteAllText(DotnetupPaths.ConfigPath, legacyJson);

        // The legacy "pathPreference" property name is no longer honored, and a config that does not
        // record an access mode does not count as completed setup.
        DotnetupConfig.Read().Should().BeNull();
        DotnetupConfig.ReadAccessMode().Should().BeNull();
        DotnetupConfig.Exists().Should().BeFalse();
    }

    [TestMethod]
    public void ReadUpdateNotificationsEnabled_DefaultsToTrue()
    {
        DotnetupConfig.ReadUpdateNotificationsEnabled().Should().BeTrue();

        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });
        DotnetupConfig.ReadUpdateNotificationsEnabled().Should().BeTrue();

        File.WriteAllText(DotnetupPaths.ConfigPath, """{ "schemaVersion": "1", "accessMode": "shell" }""");
        DotnetupConfig.ReadUpdateNotificationsEnabled().Should().BeTrue();

        File.WriteAllText(DotnetupPaths.ConfigPath, "not valid json{{{");
        DotnetupConfig.ReadUpdateNotificationsEnabled().Should().BeTrue();
    }

    [TestMethod]
    public void SetUpdateNotifications_WithoutConfig_DoesNotRecordSetup()
    {
        DotnetupConfig.SetUpdateNotifications(false);

        DotnetupConfig.ReadUpdateNotificationsEnabled().Should().BeFalse();
        DotnetupConfig.Read().Should().BeNull();
        DotnetupConfig.ReadAccessMode().Should().BeNull();
        DotnetupConfig.Exists().Should().BeFalse();
        File.ReadAllText(DotnetupPaths.ConfigPath).Should().NotContain("accessMode")
            .And.Contain("\"updateNotifications\": false");
    }

    [TestMethod]
    public void SetUpdateNotifications_PreservesSetupChoices()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = false });

        DotnetupConfig.SetUpdateNotifications(false);

        var loaded = DotnetupConfig.Read();
        loaded.Should().NotBeNull();
        loaded!.AccessMode.Should().Be(DotnetAccessMode.None);
        loaded.DotnetupOnPath.Should().BeFalse();
        loaded.UpdateNotifications.Should().BeFalse();

        DotnetupConfig.SetUpdateNotifications(true);
        DotnetupConfig.ReadUpdateNotificationsEnabled().Should().BeTrue();
    }

    [TestMethod]
    public void SetUpdateNotifications_CorruptConfig_ThrowsWithoutOverwriting()
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, "not valid json{{{");

        Action act = () => DotnetupConfig.SetUpdateNotifications(false);

        act.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.UserConfigurationCorrupted);
        File.ReadAllText(DotnetupPaths.ConfigPath).Should().Be("not valid json{{{");
    }

    [TestMethod]
    public void WriteAccessSettings_PreservesUpdateNotifications()
    {
        DotnetupConfig.SetUpdateNotifications(false);

        var written = DotnetupConfig.WriteAccessSettings(DotnetAccessMode.Everywhere, dotnetupOnPath: true);

        written.UpdateNotifications.Should().BeFalse();
        var loaded = DotnetupConfig.Read();
        loaded!.AccessMode.Should().Be(DotnetAccessMode.Everywhere);
        loaded.UpdateNotifications.Should().BeFalse();
        DotnetupConfig.Exists().Should().BeTrue();
    }

    [TestMethod]
    public void WriteAndRead_RoundTripsDotnetupOnPath()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = false });

        var loaded = DotnetupConfig.Read();

        loaded!.DotnetupOnPath.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("3")]
    [DataRow("99")]
    internal void Read_NumericAccessMode_TreatedAsCorrupt(string numericLiteral)
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, $$"""{ "schemaVersion": "1", "accessMode": {{numericLiteral}} }""");

        // Integer access-mode values are no longer accepted (allowIntegerValues: false), so a
        // numeric value is surfaced as a corrupt config.
        DotnetupConfig.Read().Should().BeNull();
    }
}
