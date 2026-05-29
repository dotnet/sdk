// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.ProjectTools.Tests;

public class VirtualProjectBuilderTests
{
    /// <summary>
    /// Verifies that <see cref="VirtualProjectBuilder.GetTempSubdirectory"/> returns a non-empty path
    /// even when <c>XDG_DATA_HOME</c> points to a directory that does not yet exist.
    /// Regression test for https://github.com/dotnet/sdk/issues/XXXXX:
    /// the BCL <c>GetFolderPath</c> overload without <c>SpecialFolderOption.Create</c>
    /// returns an empty string on Linux when <c>~/.local/share</c> is absent.
    /// </summary>
    [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.FreeBSD)]
    public void GetTempSubdirectory_ReturnsNonEmptyPath_WhenXdgDataHomeDoesNotExist()
    {
        // Use a unique subdirectory under the system temp dir that does NOT exist yet.
        string nonExistentXdgDataHome = Path.Combine(Path.GetTempPath(), $"dotnet-test-xdg-{Guid.NewGuid():N}");

        string? previousValue = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        try
        {
            Assert.False(Directory.Exists(nonExistentXdgDataHome),
                $"Test setup error: the directory '{nonExistentXdgDataHome}' already exists.");

            Environment.SetEnvironmentVariable("XDG_DATA_HOME", nonExistentXdgDataHome);

            // GetTempSubdirectory must not return an empty path, and must not throw,
            // even though XDG_DATA_HOME points to a directory that does not exist yet.
            string result = VirtualProjectBuilder.GetTempSubdirectory();

            Assert.False(string.IsNullOrEmpty(result),
                "GetTempSubdirectory() must not return an empty path when XDG_DATA_HOME does not exist.");

            // The returned path must be rooted under the new XDG_DATA_HOME.
            Assert.StartsWith(nonExistentXdgDataHome, result, StringComparison.Ordinal);
        }
        finally
        {
            // Restore the original value (null removes the variable).
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousValue);

            // Clean up any directory that was created by SpecialFolderOption.Create.
            if (Directory.Exists(nonExistentXdgDataHome))
            {
                Directory.Delete(nonExistentXdgDataHome, recursive: true);
            }
        }
    }
}
