// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class LayerTests
{
    [TestMethod]
    // Windows does not expose Unix execute permissions, so Layer.FromDirectory marks all entries executable there.
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void FromDirectorySetsFileModeBasedOnUnixExecutePermission()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory();
        string? backingFile = null;

        try
        {
            const UnixFileMode nonExecuteMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                                UnixFileMode.GroupRead |
                                                UnixFileMode.OtherRead;
            const UnixFileMode executeMode = nonExecuteMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

            string nonExecutableFilePath = Path.Join(folder.FullName, "non-executable.txt");
            string executableFilePath = Path.Join(folder.FullName, "executable.txt");
            File.WriteAllText(nonExecutableFilePath, Guid.NewGuid().ToString());
            File.WriteAllText(executableFilePath, Guid.NewGuid().ToString());

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(nonExecutableFilePath, nonExecuteMode);
                File.SetUnixFileMode(executableFilePath, executeMode);
            }

            Layer layer = Layer.FromDirectory(folder.FullName, "/app", false, SchemaTypes.DockerManifestV2);
            backingFile = layer.BackingFile;

            Dictionary<string, TarEntry> entries = LoadAllTarEntries(backingFile);
            Assert.AreEqual(nonExecuteMode, entries["app/non-executable.txt"].Mode);
            Assert.AreEqual(executeMode, entries["app/executable.txt"].Mode);
        }
        finally
        {
            if (backingFile is not null)
            {
                File.Delete(backingFile);
            }

            folder.Delete(recursive: true);
        }
    }

    private static Dictionary<string, TarEntry> LoadAllTarEntries(string file)
    {
        using var gzip = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
        using var tar = new TarReader(gzip);

        Dictionary<string, TarEntry> entries = [];
        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) is not null)
        {
            entries[entry.Name] = entry;
        }

        return entries;
    }
}
