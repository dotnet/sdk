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

        // Act
        string result = WindowsPathHelper.RemoveProgramFilesDotnetFromPath(path);

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

        // Act
        string result = WindowsPathHelper.RemoveProgramFilesDotnetFromPath(path);

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
        string path = "C:\\SomeOtherPath;C:\\AnotherPath";

        // Act
        string result = WindowsPathHelper.AddProgramFilesDotnetToPath(path);

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
        string path = $"C:\\SomeOtherPath;{dotnetPath};C:\\AnotherPath";

        // Act
        string result = WindowsPathHelper.AddProgramFilesDotnetToPath(path);

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
}
