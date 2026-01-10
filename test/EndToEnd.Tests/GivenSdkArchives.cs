// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace EndToEnd.Tests
{
    public class GivenSdkArchives(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void ItHasCorrectLinkTypesAndNoDuplicates()
        {
            // 1. Find and extract archive
            string archivePath = FindSdkArchive();
            string extractedPath = ExtractArchive(archivePath);

            // 2. Find duplicate assemblies
            var duplicateGroups = FindDuplicateAssemblies(extractedPath);

            // 3. Collect all validation errors
            var errors = new List<string>();

            // 4. Validate link types for each duplicate group
            foreach (var (hash, assemblies) in duplicateGroups)
            {
                ValidateLinkTypes(extractedPath, assemblies, errors);
            }

            // 5. Report results
            if (errors.Count > 0)
            {
                var errorMessage = new StringBuilder();
                errorMessage.AppendLine($"SDK archive validation failed with {errors.Count} error(s):");
                errorMessage.AppendLine();

                foreach (var error in errors)
                {
                    errorMessage.AppendLine(error);
                }

                Assert.Fail(errorMessage.ToString());
            }
            else
            {
                Log.WriteLine("✓ Validation passed: No issues found");
            }
        }

        private string FindSdkArchive()
        {
            // Get repo root from test context
            string? repoRoot = TestContext.Current.ToolsetUnderTest?.RepoRoot;
            if (string.IsNullOrEmpty(repoRoot))
            {
                throw new InvalidOperationException(
                    "RepoRoot not available in TestContext. Cannot locate SDK archive.");
            }

            // Archive location: {repoRoot}/artifacts/packages/{Configuration}/Shipping/
            string shippingDir = Path.Combine(repoRoot, "artifacts", "packages", GetConfiguration(), "Shipping");

            if (!Directory.Exists(shippingDir))
            {
                throw new DirectoryNotFoundException(
                    $"Shipping directory not found: {shippingDir}. " +
                    "Build the SDK first with: dotnet build /p:GenerateArchives=true");
            }

            // Find all SDK archives (.tar.gz)
            var archives = Directory.GetFiles(shippingDir, "dotnet-sdk-*.tar.gz");

            if (archives.Length == 0)
            {
                throw new FileNotFoundException(
                    $"No SDK archive found in {shippingDir}. " +
                    "Build the SDK first with: dotnet build /p:GenerateArchives=true");
            }

            string archivePath = archives.First();
            Log.WriteLine($"Found SDK archive: {Path.GetFileName(archivePath)}");
            return archivePath;
        }

        private string GetConfiguration() => Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

        private string ExtractArchive(string archivePath)
        {
            // Create isolated test directory
            var testDir = _testAssetsManager.CreateTestDirectory();
            string extractPath = Path.Combine(testDir.Path, "sdk-extracted");
            Directory.CreateDirectory(extractPath);

            Log.WriteLine($"Extracting archive to: {extractPath}");

            // Use system tar command for better compatibility with symbolic links
            var result = new RunExeCommand(Log, "tar")
                .Execute("-xzf", archivePath, "-C", extractPath)
                .Should().Pass();

            return extractPath;
        }

        private Dictionary<string, List<AssemblyInfo>> FindDuplicateAssemblies(string sdkRoot)
        {
            // Find all .dll and .exe files (same pattern as DeduplicateAssembliesWithLinks)
            var assemblies = Directory.GetFiles(sdkRoot, "*", SearchOption.AllDirectories)
                .Where(f => IsAssembly(f))
                .ToList();

            var filesByHash = new Dictionary<string, List<AssemblyInfo>>();

            // For Windows hard link detection
            Dictionary<FileIdentifier, List<string>>? fileIdMap =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new Dictionary<FileIdentifier, List<string>>()
                    : null;

            foreach (var assembly in assemblies)
            {
                try
                {
                    var info = new AssemblyInfo
                    {
                        Path = assembly,
                        Hash = ComputeFileHash(assembly),
                        Size = new FileInfo(assembly).Length,
                        IsSymlink = IsSymbolicLink(assembly),
                        IsHardLink = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? IsHardLinkWindows(assembly, fileIdMap!)
                            : false
                    };

                    if (!filesByHash.ContainsKey(info.Hash))
                    {
                        filesByHash[info.Hash] = new List<AssemblyInfo>();
                    }
                    filesByHash[info.Hash].Add(info);
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Warning: Failed to process {assembly}: {ex.Message}");
                }
            }

            // Return only groups with duplicates (same hash, multiple files)
            var duplicates = filesByHash
                .Where(kvp => kvp.Value.Count > 1)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Log.WriteLine($"Analyzed {assemblies.Count} assemblies, found {duplicates.Count} duplicate groups");
            return duplicates;
        }

        private bool IsAssembly(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        // MD5 is used for duplicate detection only, not cryptographic purposes
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "MD5 is used for file deduplication, not cryptography")]
        private string ComputeFileHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            var hashBytes = MD5.HashData(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private bool IsSymbolicLink(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            // LinkTarget will be non-null for symbolic links (even broken ones)
            // This works better than checking FileAttributes which can fail on broken links
            return fileInfo.LinkTarget != null;
        }

        private bool IsHardLinkWindows(string filePath, Dictionary<FileIdentifier, List<string>> fileIdMap)
        {
            using var handle = CreateFile(
                filePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"Failed to open file: {filePath}");
            }

            if (!GetFileInformationByHandle(handle, out var fileInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"Failed to get file information: {filePath}");
            }

            // File ID is combination of FileIndexHigh and FileIndexLow
            ulong fileIndex = ((ulong)fileInfo.FileIndexHigh << 32) | fileInfo.FileIndexLow;
            var fileId = new FileIdentifier(fileInfo.VolumeSerialNumber, fileIndex);

            // Track files by their ID
            if (!fileIdMap.ContainsKey(fileId))
            {
                fileIdMap[fileId] = new List<string>();
            }
            fileIdMap[fileId].Add(filePath);

            // Hard link if NumberOfLinks > 1
            return fileInfo.NumberOfLinks > 1;
        }

        private void ValidateLinkTypes(string sdkRoot, List<AssemblyInfo> assemblies, List<string> errors)
        {
            // Get assembly name for reporting
            string assemblyName = Path.GetFileName(assemblies[0].Path);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Expect hard links, forbid symbolic links
                bool allLinked = assemblies.All(a => a.IsHardLink || a.IsSymlink);

                if (!allLinked)
                {
                    errors.Add(
                        $"DUPLICATE: {assemblyName} appears {assemblies.Count} times without links:\n" +
                        string.Join("\n", assemblies.Select(a => $"  - {MakeRelativePath(sdkRoot, a.Path)} ({a.Size:N0} bytes)")));
                    return;
                }

                // Check for symbolic links (forbidden on Windows)
                var symlinks = assemblies.Where(a => a.IsSymlink).ToList();
                if (symlinks.Count > 0)
                {
                    errors.Add(
                        $"WRONG LINK TYPE: {assemblyName} uses symbolic links (expected hard links):\n" +
                        string.Join("\n", symlinks.Select(a => $"  - {MakeRelativePath(sdkRoot, a.Path)}")));
                }
            }
            else
            {
                // Unix/Linux: Expect all but one to be symbolic links (one master + N-1 symlinks)
                var nonSymlinks = assemblies.Where(a => !a.IsSymlink).ToList();

                if (nonSymlinks.Count == 0)
                {
                    // All are symlinks - this is wrong, need at least one master file
                    errors.Add(
                        $"ALL SYMBOLIC LINKS: {assemblyName} has all files as symbolic links (expected one master file):\n" +
                        string.Join("\n", assemblies.Select(a => $"  - {MakeRelativePath(sdkRoot, a.Path)}")));
                }
                else if (nonSymlinks.Count > 1)
                {
                    // Multiple non-symlinks - should be deduplicated
                    errors.Add(
                        $"NOT DEDUPLICATED: {assemblyName} has {nonSymlinks.Count} files that are not symbolic links (expected only 1 master file):\n" +
                        string.Join("\n", nonSymlinks.Select(a => $"  - {MakeRelativePath(sdkRoot, a.Path)}")));
                }
                // Otherwise: exactly 1 non-symlink (master) + rest are symlinks = correct!
            }
        }

        private string MakeRelativePath(string basePath, string fullPath)
        {
            // Make paths relative to SDK root for cleaner output
            if (fullPath.StartsWith(basePath))
            {
                return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        // Windows P/Invoke declarations
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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint OPEN_EXISTING = 3;

        // File identifier for tracking hard links (Windows only)
        private readonly struct FileIdentifier : IEquatable<FileIdentifier>
        {
            private readonly ulong _id1;
            private readonly ulong _id2;

            public FileIdentifier(ulong id1, ulong id2)
            {
                _id1 = id1;
                _id2 = id2;
            }

            public bool Equals(FileIdentifier other) => _id1 == other._id1 && _id2 == other._id2;
            public override bool Equals(object? obj) => obj is FileIdentifier other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_id1, _id2);
        }

        // Assembly metadata for tracking
        private class AssemblyInfo
        {
            public required string Path { get; set; }
            public required string Hash { get; set; }
            public long Size { get; set; }
            public bool IsSymlink { get; set; }
            public bool IsHardLink { get; set; }
        }
    }
}
