// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.Win32;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
[OSCondition(OperatingSystems.Windows)]
public class WindowsPathHelperTests
{
    private static readonly string s_existingDirectory = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
    private static readonly string s_otherExistingDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
    private static readonly string s_missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void GetProgramFilesDotnetPaths_ReturnsValidPaths()
    {
        // Act
        var paths = WindowsPathHelper.GetProgramFilesDotnetPaths();

        // Assert
        paths.Should().NotBeNull();
        paths.Should().NotBeEmpty();
        paths.Should().AllSatisfy(p => p.Should().EndWith("dotnet"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ReadArchitectureValues_ReadsTheValueFromEachArchitectureKey()
    {
        using var installedVersions = new TemporaryRegistryKey();
        installedVersions.SetValue("x64", "InstallLocation", @"C:\x64");
        installedVersions.SetValue("x86", "InstallLocation", @"C:\x86");
        installedVersions.SetValue(@"arm64\sharedhost", "Path", @"C:\arm64");

        WindowsPathHelper.ReadArchitectureValues(installedVersions.Key, subKeyName: null, valueName: "InstallLocation")
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["x64"] = @"C:\x64", ["x86"] = @"C:\x86" });
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ReadArchitectureValues_ReadsTheValueFromEachArchitectureSubKey()
    {
        using var installedVersions = new TemporaryRegistryKey();
        installedVersions.SetValue(@"x64\sharedhost", "Path", @"C:\x64");
        installedVersions.SetValue("x86", "InstallLocation", @"C:\x86");

        WindowsPathHelper.ReadArchitectureValues(installedVersions.Key, subKeyName: "sharedhost", valueName: "Path")
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["x64"] = @"C:\x64" });
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SelectRegisteredDotnetPaths_PrefersSharedHostPathOverInstallLocation()
    {
        WindowsPathHelper.SelectRegisteredDotnetPaths(
                sharedHostPaths: new Dictionary<string, string> { ["x64"] = s_existingDirectory },
                installLocations: new Dictionary<string, string> { ["x64"] = s_otherExistingDirectory },
                defaultPaths: [])
            .Should().Equal(s_existingDirectory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SelectRegisteredDotnetPaths_FallsBackToInstallLocationWhenSharedHostPathIsStale()
    {
        WindowsPathHelper.SelectRegisteredDotnetPaths(
                sharedHostPaths: new Dictionary<string, string> { ["x64"] = s_missingDirectory },
                installLocations: new Dictionary<string, string> { ["x64"] = s_existingDirectory + Path.DirectorySeparatorChar },
                defaultPaths: [])
            .Should().Equal(s_existingDirectory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SelectRegisteredDotnetPaths_UsesInstallLocationsWhenTheNativeViewYieldsNothing()
    {
        WindowsPathHelper.SelectRegisteredDotnetPaths(
                sharedHostPaths: new Dictionary<string, string>(),
                installLocations: new Dictionary<string, string> { ["x64"] = s_existingDirectory },
                defaultPaths: [])
            .Should().Equal(s_existingDirectory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SelectRegisteredDotnetPaths_ReadsEachArchitectureAndSkipsMissingDirectories()
    {
        WindowsPathHelper.SelectRegisteredDotnetPaths(
                sharedHostPaths: new Dictionary<string, string> { ["x64"] = s_existingDirectory },
                installLocations: new Dictionary<string, string> { ["x86"] = s_otherExistingDirectory, ["arm64"] = s_missingDirectory },
                defaultPaths: [])
            .Should().BeEquivalentTo(s_existingDirectory, s_otherExistingDirectory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SelectRegisteredDotnetPaths_FallsBackToTheDefaultRootOfUnregisteredArchitectures()
    {
        // x64 on Arm64 has no sharedhost\Path, and an unregistered install has no InstallLocation either.
        WindowsPathHelper.SelectRegisteredDotnetPaths(
                sharedHostPaths: new Dictionary<string, string> { ["arm64"] = s_existingDirectory },
                installLocations: new Dictionary<string, string>(),
                defaultPaths: [new(s_otherExistingDirectory, InstallArchitecture.x64), new(s_missingDirectory, InstallArchitecture.x86)])
            .Should().Equal(s_existingDirectory, s_otherExistingDirectory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SelectRegisteredDotnetPaths_PrefersRegistryPathsOverTheDefaultRoot()
    {
        WindowsPathHelper.SelectRegisteredDotnetPaths(
                sharedHostPaths: new Dictionary<string, string>(),
                installLocations: new Dictionary<string, string> { ["x64"] = s_existingDirectory },
                defaultPaths: [new(s_otherExistingDirectory, InstallArchitecture.x64)])
            .Should().Equal(s_existingDirectory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SelectRegisteredDotnetPaths_DoesNotDuplicateADirectorySharedByArchitectures()
    {
        WindowsPathHelper.SelectRegisteredDotnetPaths(
                sharedHostPaths: new Dictionary<string, string> { ["arm64"] = s_existingDirectory },
                installLocations: new Dictionary<string, string>(),
                defaultPaths: [new(s_existingDirectory, InstallArchitecture.arm64), new(s_existingDirectory, InstallArchitecture.x86)])
            .Should().Equal(s_existingDirectory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WarnUnreadableSystemInstallRegistry_WarnsOnce()
    {
        var denied = new SecurityException("Requested registry access is not allowed.");
        using var warnings = new StringWriter();
        int warned = 0;

        WindowsPathHelper.WarnUnreadableSystemInstallRegistry(denied, warnings, ref warned);
        WindowsPathHelper.WarnUnreadableSystemInstallRegistry(denied, warnings, ref warned);

        warnings.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().ContainSingle().Which.Should().Contain(denied.Message);
    }

    /// <summary>A temporary key under HKCU, deleted on dispose.</summary>
    private sealed class TemporaryRegistryKey : IDisposable
    {
        private readonly string _keyPath = $@"Software\dotnetup-tests\{Guid.NewGuid():N}";

        public TemporaryRegistryKey() => Key = Registry.CurrentUser.CreateSubKey(_keyPath);

        public RegistryKey Key { get; }

        public void SetValue(string subKeyPath, string name, string value)
        {
            using var key = Key.CreateSubKey(subKeyPath);
            key.SetValue(name, value);
        }

        public void Dispose()
        {
            Key.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void FindDotnetPathIndices_FindsCorrectIndices()
    {
        // Arrange
        var pathEntries = new List<string> { "C:\\Path1", "C:\\Program Files\\dotnet", "C:\\Path2", "C:\\Program Files (x86)\\dotnet" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet", "C:\\Program Files (x86)\\dotnet" };

        // Act
        var indices = WindowsPathHelper.FindPathIndices(pathEntries, dotnetPaths);

        // Assert
        indices.Should().HaveCount(2);
        indices.Should().Contain(1);
        indices.Should().Contain(3);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RemovePathEntriesByIndices_RemovesCorrectEntries()
    {
        // Arrange
        string path = "C:\\Path1;C:\\Path2;C:\\Path3;C:\\Path4";
        var indicesToRemove = new List<int> { 1, 3 };

        // Act
        string result = WindowsPathHelper.RemovePathEntriesByIndices(path, indicesToRemove);

        // Assert
        result.Should().Be("C:\\Path1;C:\\Path3");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RemovePathEntriesByIndices_HandlesEmptyIndices()
    {
        // Arrange
        string path = "C:\\Path1;C:\\Path2;C:\\Path3";
        var indicesToRemove = new List<int>();

        // Act
        string result = WindowsPathHelper.RemovePathEntriesByIndices(path, indicesToRemove);

        // Assert
        result.Should().Be(path);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void PathContainsDotnet_ReturnsTrueWhenDotnetExists()
    {
        // Arrange
        var pathEntries = new List<string> { "C:\\Path1", "C:\\Program Files\\dotnet", "C:\\Path2" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        bool result = WindowsPathHelper.PathContainsDotnet(pathEntries, dotnetPaths);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void PathContainsDotnet_ReturnsFalseWhenDotnetDoesNotExist()
    {
        // Arrange
        var pathEntries = new List<string> { "C:\\Path1", "C:\\Path2", "C:\\Path3" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        bool result = WindowsPathHelper.PathContainsDotnet(pathEntries, dotnetPaths);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void PathContainsDotnet_MatchesWhenPathEntryHasTrailingSeparator()
    {
        // Regression: the .NET installer writes the system PATH entry with a trailing separator
        // ("C:\Program Files\dotnet\"), while GetProgramFilesDotnetPaths trims it. The match must
        // ignore the trailing separator so drift detection agrees with the apply step. A raw
        // string compare here would return false and cause a permanent, unclearable "everywhere-mode
        // wiring" drift on stock machines.
        var pathEntries = new List<string> { "C:\\Path1", "C:\\Program Files\\dotnet\\", "C:\\Path2" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet" };

        bool result = WindowsPathHelper.PathContainsDotnet(pathEntries, dotnetPaths);

        result.Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void FindDotnetPathIndices_IsCaseInsensitive()
    {
        // Arrange
        var pathEntries = new List<string> { "C:\\Path1", "c:\\program files\\dotnet", "C:\\Path2" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        var indices = WindowsPathHelper.FindPathIndices(pathEntries, dotnetPaths);

        // Assert
        indices.Should().HaveCount(1);
        indices.Should().Contain(1);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RemovePathEntriesByIndices_PreservesUnexpandedVariables()
    {
        // Arrange
        string path = "%SystemRoot%\\system32;C:\\Program Files\\dotnet;%USERPROFILE%\\bin";
        var indicesToRemove = new List<int> { 1 };

        // Act
        string result = WindowsPathHelper.RemovePathEntriesByIndices(path, indicesToRemove);

        // Assert
        result.Should().Be("%SystemRoot%\\system32;%USERPROFILE%\\bin");
        result.Should().Contain("%SystemRoot%");
        result.Should().Contain("%USERPROFILE%");
    }

    // ── InsertPathEntryBeforeDotnet ──

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_InsertsImmediatelyBeforeProgramFilesDotnet()
    {
        // Arrange
        string dotnetDir = "C:\\Users\\me\\AppData\\Local\\dotnet";
        string path = "C:\\Windows;C:\\Program Files\\dotnet;C:\\tools";
        var programFilesDotnet = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        string result = WindowsPathHelper.InsertPathEntryBeforeDotnet(path, path, dotnetDir, programFilesDotnet);

        // Assert - dotnetup dir sits directly before the Program Files entry, not at the front
        result.Should().Be($"C:\\Windows;{dotnetDir};C:\\Program Files\\dotnet;C:\\tools");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_MatchesProgramFilesEntryWithTrailingSeparator()
    {
        // The .NET installer writes the system PATH entry with a trailing separator.
        string dotnetDir = "C:\\Users\\me\\AppData\\Local\\dotnet";
        string path = "C:\\Windows;C:\\Program Files\\dotnet\\;C:\\tools";
        var programFilesDotnet = new List<string> { "C:\\Program Files\\dotnet" };

        string result = WindowsPathHelper.InsertPathEntryBeforeDotnet(path, path, dotnetDir, programFilesDotnet);

        result.Should().Be($"C:\\Windows;{dotnetDir};C:\\Program Files\\dotnet\\;C:\\tools");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_AppendsToEndWhenNoProgramFilesDotnet()
    {
        // Arrange - no machine-wide dotnet on PATH
        string dotnetDir = "C:\\Users\\me\\AppData\\Local\\dotnet";
        string path = "C:\\Windows;C:\\tools";
        var programFilesDotnet = new List<string>();

        // Act
        string result = WindowsPathHelper.InsertPathEntryBeforeDotnet(path, path, dotnetDir, programFilesDotnet);

        // Assert - appended to the end so a later machine-wide install lands after it
        result.Should().Be($"C:\\Windows;C:\\tools;{dotnetDir}");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_NoChangeWhenAlreadyAheadOfProgramFilesDotnet()
    {
        // Arrange - dotnetup dir already precedes the Program Files entry
        string dotnetDir = "C:\\Users\\me\\AppData\\Local\\dotnet";
        string path = $"C:\\Windows;{dotnetDir};C:\\Program Files\\dotnet;C:\\tools";
        var programFilesDotnet = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        string result = WindowsPathHelper.InsertPathEntryBeforeDotnet(path, path, dotnetDir, programFilesDotnet);

        // Assert - unchanged
        result.Should().Be(path);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_MovesEntryWhenPresentAfterProgramFilesDotnet()
    {
        // Arrange - dotnetup dir present but AFTER the Program Files entry, so it does not win
        string dotnetDir = "C:\\Users\\me\\AppData\\Local\\dotnet";
        string path = $"C:\\Windows;C:\\Program Files\\dotnet;{dotnetDir};C:\\tools";
        var programFilesDotnet = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        string result = WindowsPathHelper.InsertPathEntryBeforeDotnet(path, path, dotnetDir, programFilesDotnet);

        // Assert - moved to immediately before the Program Files entry
        result.Should().Be($"C:\\Windows;{dotnetDir};C:\\Program Files\\dotnet;C:\\tools");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_NoChangeWhenAlreadyPresentAndNoProgramFilesDotnet()
    {
        // Arrange - already appended and no machine-wide dotnet present
        string dotnetDir = "C:\\Users\\me\\AppData\\Local\\dotnet";
        string path = $"C:\\Windows;C:\\tools;{dotnetDir}";
        var programFilesDotnet = new List<string>();

        // Act
        string result = WindowsPathHelper.InsertPathEntryBeforeDotnet(path, path, dotnetDir, programFilesDotnet);

        // Assert - unchanged
        result.Should().Be(path);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_PreservesUnexpandedVariables()
    {
        // Arrange - unexpanded PATH retains %VAR% entries
        string dotnetDir = "C:\\Users\\me\\AppData\\Local\\dotnet";
        string unexpandedPath = "%SystemRoot%\\system32;C:\\Program Files\\dotnet;%USERPROFILE%\\bin";
        string expandedPath = "C:\\Windows\\system32;C:\\Program Files\\dotnet;C:\\Users\\me\\bin";
        var programFilesDotnet = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        string result = WindowsPathHelper.InsertPathEntryBeforeDotnet(unexpandedPath, expandedPath, dotnetDir, programFilesDotnet);

        // Assert
        result.Should().Be($"%SystemRoot%\\system32;{dotnetDir};C:\\Program Files\\dotnet;%USERPROFILE%\\bin");
        result.Should().Contain("%SystemRoot%");
        result.Should().Contain("%USERPROFILE%");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AddPathEntry_ThrowsWhenEntryExpandsToMultipleSegments()
    {
        // Arrange - a PATH variable whose value itself contains ';' expands to multiple entries,
        // so the expanded and unexpanded PATH can no longer be matched by index.
        const string varName = "DOTNETUP_TEST_MULTI";
        Environment.SetEnvironmentVariable(varName, "C:\\x;C:\\y");
        try
        {
            string unexpandedPath = $"%{varName}%;C:\\bin";
            string expandedPath = "C:\\x;C:\\y;C:\\bin";

            // Act
            Action act = () => WindowsPathHelper.AddPathEntry(unexpandedPath, expandedPath, "C:\\new", "some-command");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InsertPathEntryBeforeDotnet_ThrowsWhenEntryExpandsToEmpty()
    {
        // Arrange - a PATH variable that expands to nothing drops an entry from the expanded PATH,
        // so the expanded and unexpanded PATH can no longer be matched by index.
        const string varName = "DOTNETUP_TEST_EMPTY";
        Environment.SetEnvironmentVariable(varName, " ");
        try
        {
            string unexpandedPath = $"%{varName}%;C:\\bin";
            string expandedPath = "C:\\bin";
            var programFilesDotnet = new List<string> { "C:\\Program Files\\dotnet" };

            // Act
            Action act = () => WindowsPathHelper.InsertPathEntryBeforeDotnet(unexpandedPath, expandedPath, "C:\\new", programFilesDotnet);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RemovePathEntries_ThrowsWhenEntryExpandsToMultipleSegments()
    {
        // Arrange - a PATH variable whose value itself contains ';' misaligns the two PATH forms.
        const string varName = "DOTNETUP_TEST_MULTI";
        Environment.SetEnvironmentVariable(varName, "C:\\x;C:\\y");
        try
        {
            string unexpandedPath = $"%{varName}%;C:\\bin";
            string expandedPath = "C:\\x;C:\\y;C:\\bin";
            var pathsToRemove = new List<string> { "C:\\bin" };

            // Act
            Action act = () => WindowsPathHelper.RemovePathEntries(unexpandedPath, expandedPath, pathsToRemove);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AddPathEntry_ThrowsWhenEmptyAndMultiSegmentExpansionsCancelOut()
    {
        // Arrange - one variable expands to nothing and another to multiple segments, so the
        // expanded and unexpanded PATH split into the same number of entries yet are misaligned.
        // A whole-string entry-count comparison would miss this; per-entry validation catches it.
        const string emptyVar = "DOTNETUP_TEST_EMPTY";
        const string multiVar = "DOTNETUP_TEST_MULTI";
        Environment.SetEnvironmentVariable(emptyVar, " ");
        Environment.SetEnvironmentVariable(multiVar, "C:\\x;C:\\y");
        try
        {
            string unexpandedPath = $"%{emptyVar}%;%{multiVar}%";
            string expandedPath = "C:\\x;C:\\y";

            // Act
            Action act = () => WindowsPathHelper.AddPathEntry(unexpandedPath, expandedPath, "C:\\new", "some-command");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(emptyVar, null);
            Environment.SetEnvironmentVariable(multiVar, null);
        }
    }
}
