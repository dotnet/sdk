// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

public class CopyPreservingRelativeSymlinksTests(ITestOutputHelper log) : SdkTest(log)
{
#if !NETFRAMEWORK
    [Fact]
    public void ItCopiesRegularFiles()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        var sourceFile = Path.Combine(sourceDir, "file.txt");
        var destFile = Path.Combine(destDir, "file.txt");

        File.WriteAllText(sourceFile, "test content");

        var task = CreateTask([sourceFile], [destFile]);

        task.Execute().Should().BeTrue();

        File.Exists(destFile).Should().BeTrue();
        File.ReadAllText(destFile).Should().Be("test content");
        task.CopiedFiles.Should().HaveCount(1);
    }

    [Fact]
    public void ItPreservesRelativeSymbolicLinks()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        // Create a target file and a symlink to it
        var targetFile = Path.Combine(sourceDir, "target.dll");
        var symlinkFile = Path.Combine(sourceDir, "link.dll");

        File.WriteAllText(targetFile, "target content");
        File.CreateSymbolicLink(symlinkFile, "target.dll");

        // Verify the symlink was created correctly
        new FileInfo(symlinkFile).LinkTarget.Should().Be("target.dll");

        // Copy both the target and the symlink (symlink target must be in copy scope)
        var destTarget = Path.Combine(destDir, "target.dll");
        var destSymlink = Path.Combine(destDir, "link.dll");
        var task = CreateTask([targetFile, symlinkFile], [destTarget, destSymlink]);

        task.Execute().Should().BeTrue();

        // Verify the destination is a symlink with the same relative target
        File.Exists(destSymlink).Should().BeTrue();
        var destInfo = new FileInfo(destSymlink);
        destInfo.LinkTarget.Should().Be("target.dll");
    }

    [Fact]
    public void ItPreservesRelativeSymbolicLinksWithPathTraversal()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        // Create nested structure: sourceDir/sub/link.dll -> ../target.dll
        var subDir = Path.Combine(sourceDir, "sub");
        Directory.CreateDirectory(subDir);

        var targetFile = Path.Combine(sourceDir, "target.dll");
        var symlinkFile = Path.Combine(subDir, "link.dll");

        File.WriteAllText(targetFile, "target content");
        File.CreateSymbolicLink(symlinkFile, "../target.dll");

        // Verify source symlink
        new FileInfo(symlinkFile).LinkTarget.Should().Be("../target.dll");

        // Copy to destination with same structure (both target and symlink)
        var destSubDir = Path.Combine(destDir, "sub");
        Directory.CreateDirectory(destSubDir);
        var destTarget = Path.Combine(destDir, "target.dll");
        var destSymlink = Path.Combine(destSubDir, "link.dll");

        var task = CreateTask([targetFile, symlinkFile], [destTarget, destSymlink]);

        task.Execute().Should().BeTrue();

        // Verify the relative path is preserved
        var destInfo = new FileInfo(destSymlink);
        destInfo.LinkTarget.Should().Be("../target.dll");
    }

    [Fact]
    public void ItCopiesMixedFilesAndSymlinks()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        // Create regular file
        var regularFile = Path.Combine(sourceDir, "regular.dll");
        File.WriteAllText(regularFile, "regular content");

        // Create target and symlink
        var targetFile = Path.Combine(sourceDir, "target.dll");
        var symlinkFile = Path.Combine(sourceDir, "symlink.dll");
        File.WriteAllText(targetFile, "target content");
        File.CreateSymbolicLink(symlinkFile, "target.dll");

        var destRegular = Path.Combine(destDir, "regular.dll");
        var destTarget = Path.Combine(destDir, "target.dll");
        var destSymlink = Path.Combine(destDir, "symlink.dll");

        // Copy all files including the target (symlink target must be in copy scope)
        var task = CreateTask(
            [regularFile, targetFile, symlinkFile],
            [destRegular, destTarget, destSymlink]);

        task.Execute().Should().BeTrue();

        // Regular file should be copied as regular file
        File.Exists(destRegular).Should().BeTrue();
        new FileInfo(destRegular).LinkTarget.Should().BeNull();
        File.ReadAllText(destRegular).Should().Be("regular content");

        // Symlink should be preserved as symlink
        File.Exists(destSymlink).Should().BeTrue();
        new FileInfo(destSymlink).LinkTarget.Should().Be("target.dll");
    }

    [Fact]
    public void ItCreatesDestinationDirectories()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        var sourceFile = Path.Combine(sourceDir, "file.txt");
        var destFile = Path.Combine(destDir, "nested", "deep", "file.txt");

        File.WriteAllText(sourceFile, "test content");

        var task = CreateTask([sourceFile], [destFile]);

        task.Execute().Should().BeTrue();

        File.Exists(destFile).Should().BeTrue();
    }

    [Fact]
    public void ItOverwritesExistingFiles()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        var sourceFile = Path.Combine(sourceDir, "file.txt");
        var destFile = Path.Combine(destDir, "file.txt");

        File.WriteAllText(sourceFile, "new content");
        File.WriteAllText(destFile, "old content");

        var task = CreateTask([sourceFile], [destFile]);

        task.Execute().Should().BeTrue();

        File.ReadAllText(destFile).Should().Be("new content");
    }

    [Fact]
    public void ItOverwritesExistingSymlinkWithRegularFile()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        // Source is a regular file
        var sourceFile = Path.Combine(sourceDir, "file.dll");
        File.WriteAllText(sourceFile, "regular content");

        // Destination is an existing symlink
        var destFile = Path.Combine(destDir, "file.dll");
        var dummyTarget = Path.Combine(destDir, "dummy.dll");
        File.WriteAllText(dummyTarget, "dummy");
        File.CreateSymbolicLink(destFile, "dummy.dll");

        var task = CreateTask([sourceFile], [destFile]);

        task.Execute().Should().BeTrue();

        // Should now be a regular file, not a symlink
        var destInfo = new FileInfo(destFile);
        destInfo.LinkTarget.Should().BeNull();
        File.ReadAllText(destFile).Should().Be("regular content");
    }

    [Fact]
    public void ItOverwritesExistingRegularFileWithSymlink()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        // Source is a symlink
        var targetFile = Path.Combine(sourceDir, "target.dll");
        var sourceFile = Path.Combine(sourceDir, "link.dll");
        File.WriteAllText(targetFile, "target content");
        File.CreateSymbolicLink(sourceFile, "target.dll");

        // Destination is an existing regular file
        var destTarget = Path.Combine(destDir, "target.dll");
        var destFile = Path.Combine(destDir, "link.dll");
        File.WriteAllText(destFile, "old content");

        // Copy both the target and the symlink (symlink target must be in copy scope)
        var task = CreateTask([targetFile, sourceFile], [destTarget, destFile]);

        task.Execute().Should().BeTrue();

        // Should now be a symlink
        var destInfo = new FileInfo(destFile);
        destInfo.LinkTarget.Should().Be("target.dll");
    }

    [Fact]
    public void ItFailsWhenSymlinkTargetIsOutsideCopyScope()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();
        var outsideDir = Path.Combine(Path.GetDirectoryName(sourceDir)!, "outside");
        Directory.CreateDirectory(outsideDir);

        // Create a target file outside the copy scope
        var outsideTarget = Path.Combine(outsideDir, "outside.dll");
        File.WriteAllText(outsideTarget, "outside content");

        // Create a symlink that points outside the copy scope
        var symlinkFile = Path.Combine(sourceDir, "link.dll");
        var relativePath = Path.GetRelativePath(sourceDir, outsideTarget);
        File.CreateSymbolicLink(symlinkFile, relativePath);

        var destSymlink = Path.Combine(destDir, "link.dll");

        // Only copying the symlink, not the target - should fail
        var task = CreateTask([symlinkFile], [destSymlink]);

        task.Execute().Should().BeFalse();
    }

    [Fact]
    public void ItSucceedsWhenSymlinkTargetIsWithinCopyScope()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        // Create target and symlink in source
        var targetFile = Path.Combine(sourceDir, "target.dll");
        var symlinkFile = Path.Combine(sourceDir, "link.dll");
        File.WriteAllText(targetFile, "target content");
        File.CreateSymbolicLink(symlinkFile, "target.dll");

        var destTarget = Path.Combine(destDir, "target.dll");
        var destSymlink = Path.Combine(destDir, "link.dll");

        // Copy both the symlink and its target - should succeed
        var task = CreateTask(
            [targetFile, symlinkFile],
            [destTarget, destSymlink]);

        task.Execute().Should().BeTrue();

        // Verify symlink was preserved
        new FileInfo(destSymlink).LinkTarget.Should().Be("target.dll");
    }

    [Fact]
    public void ItFailsWhenSourceFileDoesNotExist()
    {
        var (_, destDir) = CreateSourceAndDestDirs();

        var task = CreateTask(
            ["/nonexistent/file.txt"],
            [Path.Combine(destDir, "file.txt")]);

        task.Execute().Should().BeFalse();
    }

    [Fact]
    public void ItFailsWhenSourceAndDestinationCountMismatch()
    {
        var (sourceDir, destDir) = CreateSourceAndDestDirs();

        var file1 = Path.Combine(sourceDir, "file1.txt");
        var file2 = Path.Combine(sourceDir, "file2.txt");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, "content2");

        var task = CreateTask(
            [file1, file2],
            [Path.Combine(destDir, "file1.txt")]);

        task.Execute().Should().BeFalse();
    }

    [Fact]
    public void ItSucceedsWithEmptySourceFiles()
    {
        var task = CreateTask([], []);

        task.Execute().Should().BeTrue();
        task.CopiedFiles.Should().BeEmpty();
    }

    private (string sourceDir, string destDir) CreateSourceAndDestDirs()
    {
        var testDir = TestAssetsManager.CreateTestDirectory().Path;
        var sourceDir = Path.Combine(testDir, "source");
        var destDir = Path.Combine(testDir, "dest");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);
        return (sourceDir, destDir);
    }

    private static CopyPreservingRelativeSymlinks CreateTask(string[] sourceFiles, string[] destinationFiles)
    {
        return new CopyPreservingRelativeSymlinks
        {
            SourceFiles = sourceFiles.Select(f => new TaskItem(f)).ToArray(),
            DestinationFiles = destinationFiles.Select(f => new TaskItem(f)).ToArray(),
            BuildEngine = new MockBuildEngine()
        };
    }
#endif
}
