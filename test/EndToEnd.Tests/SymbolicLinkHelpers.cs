// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace EndToEnd.Tests
{
    /// <summary>
    /// Shared helpers for verifying symbolic links in tests.
    /// </summary>
    internal static class SymbolicLinkHelpers
    {
        /// <summary>
        /// Verifies that a directory contains >100 symbolic links and all use relative paths.
        /// </summary>
        /// <param name="directory">The directory to check for symbolic links.</param>
        /// <param name="log">Test output logger.</param>
        /// <param name="contextName">Name of the context being tested (for error messages, e.g., "deb package", "archive").</param>
        public static void VerifyDirectoryHasRelativeSymlinks(string directory, ITestOutputHelper log, string contextName)
        {
            // Find all symbolic links in the directory
            var result = new RunExeCommand(log, "find")
                .WithWorkingDirectory(directory)
                .Execute(".", "-type", "l")
                .Should().Pass();

            var symlinkPaths = result.StdOut
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            log.WriteLine($"Found {symlinkPaths.Count} symbolic links in {contextName}");

            // Verify deduplication worked: expect > 100 symlinks
            Assert.True(symlinkPaths.Count > 100,
                $"Expected more than 100 symbolic links in {contextName}, but found only {symlinkPaths.Count}. " +
                "This suggests deduplication did not run correctly.");

            // Verify all symlinks use relative paths (not absolute)
            var absoluteSymlinks = new List<string>();
            foreach (var symlinkPath in symlinkPaths)
            {
                var fullPath = Path.Combine(directory, symlinkPath.TrimStart('.', '/'));
                var readlinkResult = new RunExeCommand(log, "readlink")
                    .Execute(fullPath)
                    .Should().Pass();

                var target = readlinkResult.StdOut.Trim();
                if (target.StartsWith("/"))
                {
                    absoluteSymlinks.Add($"{symlinkPath} -> {target}");
                }
            }

            Assert.Empty(absoluteSymlinks);
            log.WriteLine($"Verified all {symlinkPaths.Count} symbolic links use relative paths");
        }
    }
}
