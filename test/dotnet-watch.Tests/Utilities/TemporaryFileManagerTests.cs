// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;

namespace Microsoft.DotNet.Watch.UnitTests;

public class TemporaryFileManagerTests
{
    [Fact]
    public void CreateFile_IsOwnerOnlyAndRemovedOnDispose()
    {
        string path1;
        string path2;
        string directory;
        using (var manager = new TemporaryFileManager())
        {
            using (var file1 = manager.Create(".slnx", stream => stream.Write("<Solution />"u8)))
            using (var file2 = manager.Create(".tmp", _ => { }))
            {
                path1 = file1.Path;
                path2 = file2.Path;

                directory = Path.GetDirectoryName(path1)!;
                Assert.Equal(directory, Path.GetDirectoryName(path2));
                Assert.Equal("1.slnx", Path.GetFileName(path1));
                Assert.Equal("2.tmp", Path.GetFileName(path2));
                Assert.True(File.Exists(path1));
                Assert.True(File.Exists(path2));
                Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(path1), PathComparison);

                // MSBuild reads solution files using File.OpenRead, which requests FileShare.Read.
                using (var reader = new StreamReader(File.OpenRead(path1)))
                {
                    Assert.Equal("<Solution />", reader.ReadToEnd());
                }

                if (OperatingSystem.IsWindows())
                {
                    Assert.Throws<IOException>(() => new FileStream(path1, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
                }
                else
                {
                    AssertUnixOwnerOnly(directory);
                }
            }

            Assert.False(File.Exists(path1));
            Assert.False(File.Exists(path2));
        }

        Assert.False(Directory.Exists(directory));
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    [UnsupportedOSPlatform("windows")]
    private static void AssertUnixOwnerOnly(string path)
    {
        var mode = File.GetUnixFileMode(path);
        const UnixFileMode nonOwnerBits =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        Assert.Equal(UnixFileMode.None, mode & nonOwnerBits);
    }
}
