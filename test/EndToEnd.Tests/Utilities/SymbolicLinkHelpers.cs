// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace EndToEnd.Tests.Utilities;

/// <summary>
/// Shared helpers for verifying symbolic links and extracting archives in tests.
/// </summary>
internal static class SymbolicLinkHelpers
{
    /// <summary>
    /// Minimum number of deduplicated links expected in an SDK layout.
    /// Used by both symbolic link and hard link validation.
    /// </summary>
    public const int MinExpectedDeduplicatedLinks = 100;

    /// <summary>
    /// Extracts a tar.gz archive to a directory using system tar command.
    /// Uses system tar for simplicity.
    /// </summary>
    /// <param name="tarGzPath">Path to the .tar.gz file to extract.</param>
    /// <param name="destinationDirectory">Directory to extract files into.</param>
    /// <param name="log">Test output logger.</param>
    public static void ExtractTarGz(string tarGzPath, string destinationDirectory, ITestOutputHelper log)
    {
        new RunExeCommand(log, "tar")
            .Execute("-xzf", tarGzPath, "-C", destinationDirectory)
            .Should().Pass();
    }

    /// <summary>
    /// Extracts an installer package to a temporary directory, verifies symbolic links, and cleans up.
    /// </summary>
    /// <param name="installerFile">Path to the installer file.</param>
    /// <param name="packageType">Type of package (e.g., "deb", "rpm", "pkg") for logging.</param>
    /// <param name="extractPackage">Action that extracts the package contents to the provided temp directory.</param>
    /// <param name="log">Test output logger.</param>
    public static void VerifyPackageSymlinks(string installerFile, string packageType, Action<string> extractPackage, ITestOutputHelper log)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"{packageType}-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            extractPackage(tempDir);
            VerifyDirectoryHasRelativeSymlinks(tempDir, log, $"{packageType} package");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that a directory contains >100 symbolic links and all use relative paths.
    /// </summary>
    /// <param name="directory">The directory to check for symbolic links.</param>
    /// <param name="log">Test output logger.</param>
    /// <param name="contextName">Name of the context being tested (for error messages, e.g., "deb package", "archive").</param>
    public static void VerifyDirectoryHasRelativeSymlinks(string directory, ITestOutputHelper log, string contextName)
    {
        // Find all symbolic links in the directory
        var findResult = new RunExeCommand(log, "find")
            .WithWorkingDirectory(directory)
            .Execute(".", "-type", "l");

        findResult.Should().Pass();

        var symlinkPaths = (findResult.StdOut ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        log.WriteLine($"Found {symlinkPaths.Count} symbolic links in {contextName}");

        Assert.True(symlinkPaths.Count > MinExpectedDeduplicatedLinks,
            $"Expected more than {MinExpectedDeduplicatedLinks} symbolic links in {contextName}, but found only {symlinkPaths.Count}. " +
            "This suggests deduplication did not run correctly.");

        // Verify all symlinks use relative paths (not absolute)
        var absoluteSymlinks = new List<string>();
        foreach (var symlinkPath in symlinkPaths)
        {
            var fullPath = Path.Combine(directory, symlinkPath.TrimStart('.', '/'));
            var readlinkResult = new RunExeCommand(log, "readlink")
                .Execute(fullPath);

            readlinkResult.Should().Pass();

            var target = (readlinkResult.StdOut ?? string.Empty).Trim();
            if (target.StartsWith("/"))
            {
                absoluteSymlinks.Add($"{symlinkPath} -> {target}");
            }
        }

        Assert.Empty(absoluteSymlinks);
        log.WriteLine($"Verified all {symlinkPaths.Count} symbolic links use relative paths");
    }
}
