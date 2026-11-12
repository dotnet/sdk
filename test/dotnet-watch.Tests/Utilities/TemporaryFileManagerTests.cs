// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class TemporaryFileManagerTests
{
    [TestMethod]
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
                Assert.AreEqual(directory, Path.GetDirectoryName(path2));
                Assert.AreEqual("1.slnx", Path.GetFileName(path1));
                Assert.AreEqual("2.tmp", Path.GetFileName(path2));
                Assert.IsTrue(File.Exists(path1));
                Assert.IsTrue(File.Exists(path2));
                Assert.IsTrue(Path.GetFullPath(path1).StartsWith(Path.GetFullPath(Path.GetTempPath()), PathComparison));

                // MSBuild reads solution files using File.OpenRead, which requests FileShare.Read.
                using (var reader = new StreamReader(File.OpenRead(path1)))
                {
                    Assert.AreEqual("<Solution />", reader.ReadToEnd());
                }

                if (OperatingSystem.IsWindows())
                {
                    Assert.ThrowsExactly<IOException>(() => new FileStream(path1, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
                }
                else
                {
                    AssertUnixOwnerOnly(directory);
                }
            }

            Assert.IsFalse(File.Exists(path1));
            Assert.IsFalse(File.Exists(path2));
        }

        Assert.IsFalse(Directory.Exists(directory));
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
        Assert.AreEqual(UnixFileMode.None, mode & nonOwnerBits);
    }
}
