// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public class WindowsPathHelperTests
{
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
}
