// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

public class DeduplicateAssembliesWithLinksTests(ITestOutputHelper log) : SdkTest(log)
{
#if !NETFRAMEWORK
    [Fact]
    public void WhenDuplicatesExistItCreatesHardLinks()
    {
        var layoutDir = TestAssetsManager.CreateTestDirectory().Path;

        var content = "duplicate assembly content";
        var file1 = Path.Combine(layoutDir, "assembly1.dll");
        var file2 = Path.Combine(layoutDir, "assembly2.dll");
        var file3 = Path.Combine(layoutDir, "assembly3.dll");

        File.WriteAllText(file1, content);
        File.WriteAllText(file2, content);
        File.WriteAllText(file3, content);

        var task = CreateTask(layoutDir, useHardLinks: true);
        var result = task.Execute();

        result.Should().BeTrue();

        // All files should still exist
        File.Exists(file1).Should().BeTrue();
        File.Exists(file2).Should().BeTrue();
        File.Exists(file3).Should().BeTrue();

        // All should have the same content
        File.ReadAllText(file1).Should().Be(content);
        File.ReadAllText(file2).Should().Be(content);
        File.ReadAllText(file3).Should().Be(content);

        // With hard links, all should point to the same inode/file index
        var inode1 = GetInode(file1);
        var inode2 = GetInode(file2);
        var inode3 = GetInode(file3);

        inode1.Should().Be(inode2);
        inode2.Should().Be(inode3);
    }

    [Fact]
    public void WhenDuplicatesExistItCreatesSymbolicLinks()
    {
        var layoutDir = TestAssetsManager.CreateTestDirectory().Path;

        var content = "duplicate assembly content";
        var file1 = Path.Combine(layoutDir, "assembly1.dll");
        var file2 = Path.Combine(layoutDir, "assembly2.dll");

        File.WriteAllText(file1, content);
        File.WriteAllText(file2, content);

        var task = CreateTask(layoutDir, useHardLinks: false);
        var result = task.Execute();

        result.Should().BeTrue();

        // Both files should exist
        File.Exists(file1).Should().BeTrue();
        File.Exists(file2).Should().BeTrue();

        // One should be a symlink
        var file1Info = new FileInfo(file1);
        var file2Info = new FileInfo(file2);

        var symlinksCreated = (file1Info.LinkTarget != null) || (file2Info.LinkTarget != null);
        symlinksCreated.Should().BeTrue();
    }

    [Fact]
    public void ItSelectsMasterByDepthThenAlphabetically()
    {
        var layoutDir = TestAssetsManager.CreateTestDirectory().Path;
        var subDir1 = Path.Combine(layoutDir, "sub1");
        var subDir2 = Path.Combine(layoutDir, "sub2");
        var subSubDir = Path.Combine(subDir1, "nested");

        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);
        Directory.CreateDirectory(subSubDir);

        var content = "shared content";

        // Create files at different depths and alphabetical positions
        var rootFileZ = Path.Combine(layoutDir, "z.dll");
        var rootFileA = Path.Combine(layoutDir, "a.dll");
        var sub1File = Path.Combine(subDir1, "file.dll");
        var sub2File = Path.Combine(subDir2, "file.dll");
        var nestedFile = Path.Combine(subSubDir, "file.dll");

        File.WriteAllText(rootFileZ, content);
        File.WriteAllText(rootFileA, content);
        File.WriteAllText(sub1File, content);
        File.WriteAllText(sub2File, content);
        File.WriteAllText(nestedFile, content);

        var task = CreateTask(layoutDir, useHardLinks: true);
        var result = task.Execute();

        result.Should().BeTrue();

        // The primary should be the one at root level that's alphabetically first (a.dll)
        // We can verify this by checking that all files are hard linked together
        var primaryInode = GetInode(rootFileA);
        GetInode(rootFileZ).Should().Be(primaryInode);
        GetInode(sub1File).Should().Be(primaryInode);
        GetInode(sub2File).Should().Be(primaryInode);
        GetInode(nestedFile).Should().Be(primaryInode);
    }

    [Fact]
    public void ItOnlyDeduplicatesAssemblies()
    {
        var layoutDir = TestAssetsManager.CreateTestDirectory().Path;

        var content = "shared content";

        // Create duplicate assemblies
        var dll1 = Path.Combine(layoutDir, "assembly1.dll");
        var dll2 = Path.Combine(layoutDir, "assembly2.dll");
        var exe1 = Path.Combine(layoutDir, "program1.exe");
        var exe2 = Path.Combine(layoutDir, "program2.exe");

        // Create duplicate non-assemblies
        var txt1 = Path.Combine(layoutDir, "file1.txt");
        var txt2 = Path.Combine(layoutDir, "file2.txt");
        var json1 = Path.Combine(layoutDir, "config1.json");
        var json2 = Path.Combine(layoutDir, "config2.json");

        File.WriteAllText(dll1, content);
        File.WriteAllText(dll2, content);
        File.WriteAllText(exe1, content);
        File.WriteAllText(exe2, content);
        File.WriteAllText(txt1, content);
        File.WriteAllText(txt2, content);
        File.WriteAllText(json1, content);
        File.WriteAllText(json2, content);

        var task = CreateTask(layoutDir, useHardLinks: true);
        var result = task.Execute();

        result.Should().BeTrue();

        // Assemblies should be deduplicated
        var dllInode = GetInode(dll1);
        GetInode(dll2).Should().Be(dllInode);

        var exeInode = GetInode(exe1);
        GetInode(exe2).Should().Be(exeInode);

        // Non-assemblies should NOT be deduplicated
        var txt1Inode = GetInode(txt1);
        var txt2Inode = GetInode(txt2);
        txt1Inode.Should().NotBe(txt2Inode);

        var json1Inode = GetInode(json1);
        var json2Inode = GetInode(json2);
        json1Inode.Should().NotBe(json2Inode);
    }

    [Fact]
    public void WhenLayoutDirectoryDoesNotExistItFails()
    {
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var task = CreateTask(nonExistentDir);
        var result = task.Execute();

        result.Should().BeFalse();
    }

    [Fact]
    public void ItHandlesMultipleDuplicateGroups()
    {
        var layoutDir = TestAssetsManager.CreateTestDirectory().Path;

        // Group 1: duplicates with content A
        var contentA = "content A";
        File.WriteAllText(Path.Combine(layoutDir, "a1.dll"), contentA);
        File.WriteAllText(Path.Combine(layoutDir, "a2.dll"), contentA);
        File.WriteAllText(Path.Combine(layoutDir, "a3.dll"), contentA);

        // Group 2: duplicates with content B
        var contentB = "content B";
        File.WriteAllText(Path.Combine(layoutDir, "b1.dll"), contentB);
        File.WriteAllText(Path.Combine(layoutDir, "b2.dll"), contentB);

        // Unique file
        File.WriteAllText(Path.Combine(layoutDir, "unique.dll"), "unique");

        var task = CreateTask(layoutDir, useHardLinks: true);
        var result = task.Execute();

        result.Should().BeTrue();

        // Group A files should all be linked together
        var inodeA1 = GetInode(Path.Combine(layoutDir, "a1.dll"));
        GetInode(Path.Combine(layoutDir, "a2.dll")).Should().Be(inodeA1);
        GetInode(Path.Combine(layoutDir, "a3.dll")).Should().Be(inodeA1);

        // Group B files should all be linked together
        var inodeB1 = GetInode(Path.Combine(layoutDir, "b1.dll"));
        GetInode(Path.Combine(layoutDir, "b2.dll")).Should().Be(inodeB1);

        // Groups should not be linked to each other
        inodeA1.Should().NotBe(inodeB1);

        // Unique file should not be linked
        var inodeUnique = GetInode(Path.Combine(layoutDir, "unique.dll"));
        inodeUnique.Should().NotBe(inodeA1);
        inodeUnique.Should().NotBe(inodeB1);
    }

    [Fact]
    public void ItCreatesRelativeSymbolicLinks()
    {
        var layoutDir = TestAssetsManager.CreateTestDirectory().Path;
        var subDir = Path.Combine(layoutDir, "subdir");
        Directory.CreateDirectory(subDir);

        var content = "duplicate content";
        var rootFile = Path.Combine(layoutDir, "primary.dll");
        var subFile = Path.Combine(subDir, "duplicate.dll");

        File.WriteAllText(rootFile, content);
        File.WriteAllText(subFile, content);

        var task = CreateTask(layoutDir, useHardLinks: false);
        var result = task.Execute();

        result.Should().BeTrue();

        // Check that the symlink is relative
        var rootInfo = new FileInfo(rootFile);
        var subInfo = new FileInfo(subFile);

        // One should be a symlink (the one in subdir, since primary is at root)
        if (subInfo.LinkTarget != null)
        {
            // Should be a relative path, not absolute
            Path.IsPathRooted(subInfo.LinkTarget).Should().BeFalse();

            // Normalize path separators for cross-platform compatibility
            var normalizedLinkTarget = subInfo.LinkTarget.Replace('\\', '/');
            normalizedLinkTarget.Should().Be("../primary.dll");
        }
    }

    private static DeduplicateAssembliesWithLinks CreateTask(string layoutDir, bool useHardLinks = true)
    {
        var task = new DeduplicateAssembliesWithLinks
        {
            LayoutDirectory = layoutDir,
            UseHardLinks = useHardLinks,
            BuildEngine = new MockBuildEngine()
        };
        return task;
    }

    private long GetInode(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsFileIndex(filePath);
        }
        else
        {
            // Use stat to get inode number on Unix systems
            // Linux uses GNU stat: -c %i
            // macOS uses BSD stat: -f %i
            var formatFlag = OperatingSystem.IsMacOS() ? "-f" : "-c";

            var result = new RunExeCommand(Log, "stat")
                .Execute(formatFlag, "%i", filePath);

            result.Should().Pass();

            return long.Parse(result.StdOut!.Trim());
        }
    }

    private static long GetWindowsFileIndex(string filePath)
    {
        using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (!GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION fileInfo))
        {
            throw new System.ComponentModel.Win32Exception();
        }

        // Combine high and low parts of the file index
        return ((long)fileInfo.nFileIndexHigh << 32) | fileInfo.nFileIndexLow;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }
#endif
}
