// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EndToEnd.Tests
{
    public class GivenSdkArchives(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void ItHasDeduplicatedAssemblies()
        {
            // Find and extract archive
            string archivePath = FindSdkArchive();
            string extractedPath = ExtractArchive(archivePath);

            // Verify deduplication worked by checking for links
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                VerifyWindowsHardLinks(extractedPath);
            }
            else
            {
                VerifyLinuxSymbolicLinks(extractedPath);
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

        private void VerifyWindowsHardLinks(string extractedPath)
        {
            // Find all assemblies (.dll and .exe files)
            var assemblies = Directory.GetFiles(extractedPath, "*", SearchOption.AllDirectories)
                .Where(f => IsAssembly(f))
                .ToList();

            Log.WriteLine($"Found {assemblies.Count} total assemblies in archive");

            // Count assemblies that are hard linked (NumberOfLinks > 1)
            int hardLinkCount = 0;
            foreach (var assembly in assemblies)
            {
                try
                {
                    if (IsHardLinked(assembly))
                    {
                        hardLinkCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Warning: Failed to check {assembly}: {ex.Message}");
                }
            }

            Log.WriteLine($"Found {hardLinkCount} hard linked assemblies");

            // Verify deduplication worked: expect > 100 hard links
            Assert.True(hardLinkCount > 100,
                $"Expected more than 100 hard linked assemblies, but found only {hardLinkCount}. " +
                "This suggests deduplication did not run correctly.");
        }

        private void VerifyLinuxSymbolicLinks(string extractedPath)
        {
            SymbolicLinkHelpers.VerifyDirectoryHasRelativeSymlinks(extractedPath, Log, "archive");
        }

        private static bool IsAssembly(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHardLinked(string filePath)
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

            // Hard link if NumberOfLinks > 1
            return fileInfo.NumberOfLinks > 1;
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
    }
}
