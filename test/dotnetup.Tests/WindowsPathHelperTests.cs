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
    public void RemoveProgramFilesDotnetFromPath_RemovesCorrectPath()
    {
        // Arrange
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string dotnetPath = Path.Combine(programFiles, "dotnet");
        string path = $"C:\\SomeOtherPath;{dotnetPath};C:\\AnotherPath";

        // Act - pass the same path for both since no environment variables are used
        string result = WindowsPathHelper.RemoveProgramFilesDotnetFromPath(path, path);

        // Assert
        result.Should().NotContain(dotnetPath);
        result.Should().Contain("C:\\SomeOtherPath");
        result.Should().Contain("C:\\AnotherPath");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RemoveProgramFilesDotnetFromPath_HandlesEmptyPath()
    {
        // Arrange
        string path = string.Empty;

        // Act - pass the same path for both since no environment variables are used
        string result = WindowsPathHelper.RemoveProgramFilesDotnetFromPath(path, path);

        // Assert
        result.Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AddProgramFilesDotnetToPath_AddsCorrectPath()
    {
        // Arrange
        string unexpandedPath = "C:\\SomeOtherPath;C:\\AnotherPath";
        string expandedPath = unexpandedPath; // No environment variables to expand in test

        // Act
        string result = WindowsPathHelper.AddProgramFilesDotnetToPath(unexpandedPath, expandedPath);

        // Assert
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string dotnetPath = Path.Combine(programFiles, "dotnet");
        result.Should().Contain(dotnetPath);
        result.Should().Contain("C:\\SomeOtherPath");
        result.Should().Contain("C:\\AnotherPath");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AddProgramFilesDotnetToPath_DoesNotAddDuplicatePath()
    {
        // Arrange
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string dotnetPath = Path.Combine(programFiles, "dotnet");
        string unexpandedPath = $"C:\\SomeOtherPath;{dotnetPath};C:\\AnotherPath";
        string expandedPath = unexpandedPath; // No environment variables to expand in test

        // Act
        string result = WindowsPathHelper.AddProgramFilesDotnetToPath(unexpandedPath, expandedPath);

        // Assert
        // Count occurrences of dotnetPath in result
        int count = result.Split(';').Count(p => p.Equals(dotnetPath, StringComparison.OrdinalIgnoreCase));
        count.Should().Be(1);
    }

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
}
