// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class WindowsPathHelperTests
{
    [Fact]
    public void RemoveProgramFilesDotnetFromPath_RemovesCorrectPath()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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

    [Fact]
    public void RemoveProgramFilesDotnetFromPath_HandlesEmptyPath()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        string path = string.Empty;

        // Act - pass the same path for both since no environment variables are used
        string result = WindowsPathHelper.RemoveProgramFilesDotnetFromPath(path, path);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AddProgramFilesDotnetToPath_AddsCorrectPath()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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

    [Fact]
    public void AddProgramFilesDotnetToPath_DoesNotAddDuplicatePath()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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

    [Fact]
    public void GetProgramFilesDotnetPaths_ReturnsValidPaths()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Act
        var paths = WindowsPathHelper.GetProgramFilesDotnetPaths();

        // Assert
        paths.Should().NotBeNull();
        paths.Should().NotBeEmpty();
        paths.Should().AllSatisfy(p => p.Should().EndWith("dotnet"));
    }

    [Fact]
    public void FindDotnetPathIndices_FindsCorrectIndices()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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

    [Fact]
    public void RemovePathEntriesByIndices_RemovesCorrectEntries()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        string path = "C:\\Path1;C:\\Path2;C:\\Path3;C:\\Path4";
        var indicesToRemove = new List<int> { 1, 3 };

        // Act
        string result = WindowsPathHelper.RemovePathEntriesByIndices(path, indicesToRemove);

        // Assert
        result.Should().Be("C:\\Path1;C:\\Path3");
    }

    [Fact]
    public void RemovePathEntriesByIndices_HandlesEmptyIndices()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        string path = "C:\\Path1;C:\\Path2;C:\\Path3";
        var indicesToRemove = new List<int>();

        // Act
        string result = WindowsPathHelper.RemovePathEntriesByIndices(path, indicesToRemove);

        // Assert
        result.Should().Be(path);
    }

    [Fact]
    public void PathContainsDotnet_ReturnsTrueWhenDotnetExists()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var pathEntries = new List<string> { "C:\\Path1", "C:\\Program Files\\dotnet", "C:\\Path2" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        bool result = WindowsPathHelper.PathContainsDotnet(pathEntries, dotnetPaths);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void PathContainsDotnet_ReturnsFalseWhenDotnetDoesNotExist()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var pathEntries = new List<string> { "C:\\Path1", "C:\\Path2", "C:\\Path3" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        bool result = WindowsPathHelper.PathContainsDotnet(pathEntries, dotnetPaths);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FindDotnetPathIndices_IsCaseInsensitive()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var pathEntries = new List<string> { "C:\\Path1", "c:\\program files\\dotnet", "C:\\Path2" };
        var dotnetPaths = new List<string> { "C:\\Program Files\\dotnet" };

        // Act
        var indices = WindowsPathHelper.FindPathIndices(pathEntries, dotnetPaths);

        // Assert
        indices.Should().HaveCount(1);
        indices.Should().Contain(1);
    }

    [Fact]
    public void RemovePathEntriesByIndices_PreservesUnexpandedVariables()
    {
        // This test can only run on Windows where WindowsPathHelper is supported
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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
