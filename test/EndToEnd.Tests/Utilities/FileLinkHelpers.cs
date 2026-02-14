// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EndToEnd.Tests.Utilities;

/// <summary>
/// Shared helpers for verifying file links (symbolic links and hardlinks) and extracting archives in tests.
/// </summary>
internal static class FileLinkHelpers
{
    /// <summary>
    /// Minimum number of deduplicated links expected in an SDK layout.
    /// Used by both symbolic link and hard link validation.
    /// </summary>
    public const int MinExpectedDeduplicatedLinks = 100;

    /// <summary>
    /// Extracts a tar archive to a directory using system tar command.
    /// Auto-detects compression format (.gz, .xz, .zst, etc.).
    /// </summary>
    /// <param name="tarPath">Path to the tar archive to extract.</param>
    /// <param name="destinationDirectory">Directory to extract files into.</param>
    /// <param name="log">Test output logger.</param>
    public static void ExtractTar(string tarPath, string destinationDirectory, ITestOutputHelper log) =>
        new RunExeCommand(log, "tar")
            .Execute("-xf", tarPath, "-C", destinationDirectory)
            .Should().Pass();

    /// <summary>
    /// Verifies that an installer package preserves symbolic links. Handles platform checking,
    /// artifact discovery, extraction, and symlink validation.
    /// </summary>
    /// <param name="requiredPlatform">The platform this test should run on (e.g., OSPlatform.Linux).</param>
    /// <param name="filePattern">The file pattern to search for (e.g., "dotnet-sdk-*.deb").</param>
    /// <param name="excludeSubstrings">Substrings to exclude from filenames when finding artifacts.</param>
    /// <param name="extractPackage">Action that extracts the package. Receives (installerPath, tempDir).</param>
    /// <param name="testAssetsManager">Test assets manager for creating temp directories.</param>
    /// <param name="log">Test output logger.</param>
    public static void VerifyInstallerSymlinks(
        OSPlatform requiredPlatform,
        string filePattern,
        string[] excludeSubstrings,
        Action<string, string> extractPackage,
        TestAssetsManager testAssetsManager,
        ITestOutputHelper log)
    {
        if (!RuntimeInformation.IsOSPlatform(requiredPlatform))
        {
            log.WriteLine($"SKIPPED: Test requires {requiredPlatform} but running on {RuntimeInformation.OSDescription}");
            return;
        }

        if (!SdkTestContext.FindOptionalSdkAcquisitionArtifact(filePattern, excludeSubstrings, out string? installerPath))
        {
            log.WriteLine($"SKIPPED: No artifact matching '{filePattern}' found in shipping packages directory");
            return;
        }

        log.WriteLine($"Validating: {Path.GetFileName(installerPath)}");
        var packageType = Path.GetExtension(installerPath!).TrimStart('.');
        var tempDir = testAssetsManager.CreateTestDirectory(packageType).Path;

        extractPackage(installerPath!, tempDir);
        VerifyDirectoryHasRelativeSymlinks(tempDir, log, packageType);
    }

    /// <summary>
    /// Verifies that a directory contains >100 symbolic links and all use relative paths.
    /// </summary>
    /// <param name="directory">The directory to check for symbolic links.</param>
    /// <param name="log">Test output logger.</param>
    /// <param name="contextName">Name of the context being tested (for error messages, e.g., "deb", "archive").</param>
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

    /// <summary>
    /// Verifies that a directory contains >100 hardlinked files (files with link count > 1).
    /// Used on Windows where hardlinks are used instead of symbolic links for deduplication.
    /// </summary>
    /// <param name="directory">The directory to check for hardlinks.</param>
    /// <param name="log">Test output logger.</param>
    /// <param name="contextName">Name of the context being tested (for error messages, e.g., "archive").</param>
    public static void VerifyDirectoryHasHardlinks(string directory, ITestOutputHelper log, string contextName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException($"{nameof(VerifyDirectoryHasHardlinks)} is only supported on Windows.");
        }

        var hardlinkedFiles = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            uint linkCount = GetFileLinkCount(filePath);
            if (linkCount > 1)
            {
                hardlinkedFiles.Add(filePath);
            }
        }

        log.WriteLine($"Found {hardlinkedFiles.Count} hardlinked files in {contextName}");

        Assert.True(hardlinkedFiles.Count > MinExpectedDeduplicatedLinks,
            $"Expected more than {MinExpectedDeduplicatedLinks} hardlinked files in {contextName}, but found only {hardlinkedFiles.Count}. " +
            "This suggests deduplication did not run correctly.");

        log.WriteLine($"Verified {hardlinkedFiles.Count} files have hardlinks (link count > 1)");
    }

    /// <summary>
    /// Gets the number of hardlinks to a file on Windows.
    /// </summary>
    private static uint GetFileLinkCount(string filePath)
    {
        using var handle = CreateFile(
            filePath,
            0, // No access required, just querying info
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return 1; // Assume single link if we can't query
        }

        if (GetFileInformationByHandle(handle, out var fileInfo))
        {
            return fileInfo.NumberOfLinks;
        }

        return 1; // Assume single link if query fails
    }

    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
