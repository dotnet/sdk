// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class LayerTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task Cancellation_after_temporary_layer_creation_removes_partial_content()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        Task? creation = null;
        Task? observation = null;
        using CancellationTokenSource stopObservation = new();
        try
        {
            string source = Path.Combine(root, "source");
            string cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(cache);
            byte[] payload = new byte[24 * 1024 * 1024];
            new Random(42).NextBytes(payload);
            await File.WriteAllBytesAsync(Path.Combine(source, "app"), payload, TestContext.CancellationToken);
            TaskCompletionSource observing = new(TaskCreationOptions.RunContinuationsAsynchronously);
            // FileSystemWatcher callbacks can wait behind compression work on the
            // thread pool. Observe the actual partial file on a dedicated thread.
            observation = Task.Factory.StartNew(() =>
            {
                observing.SetResult();
                while (!stopObservation.IsCancellationRequested)
                {
                    if (Directory.EnumerateFiles(cache, "*.tmp").Any())
                    {
                        cancellation.Cancel();
                        return;
                    }
                    Thread.Sleep(1);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await observing.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
            creation = Task.Run(() => Layer.FromDirectory(source, "/app", false, SchemaTypes.OciManifestV1, null,
                DateTimeOffset.UnixEpoch, cache, cancellation.Token), TestContext.CancellationToken);
            await observation.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
            await Assert.ThrowsAsync<OperationCanceledException>(() => creation);
            Assert.IsEmpty(Directory.GetFiles(cache));
        }
        finally
        {
            cancellation.Cancel();
            stopObservation.Cancel();
            if (observation is not null) await observation;
            if (creation is not null) await creation.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Layer_descriptors_match_all_compressed_and_uncompressed_bytes(bool windowsLayer)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string source = Path.Combine(root, "source");
            string cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(source);
            byte[] payload = new byte[3 * 1024 * 1024];
            new Random(42).NextBytes(payload);
            File.WriteAllBytes(Path.Combine(source, "app"), payload);
            Layer layer = Layer.FromDirectory(source, "/app", windowsLayer, SchemaTypes.OciManifestV1, null,
                DateTimeOffset.UnixEpoch, cache, TestContext.CancellationToken);
            using FileStream compressed = File.OpenRead(layer.BackingFile);
            Assert.AreEqual(compressed.Length, layer.Descriptor.Size);
            byte[] compressedDigest = System.Security.Cryptography.SHA256.HashData(compressed);
            Assert.AreEqual(DigestUtils.FormatSha256Digest(Convert.ToHexStringLower(compressedDigest)), layer.Descriptor.Digest);
            compressed.Position = 0;
            using GZipStream decompressed = new(compressed, CompressionMode.Decompress);
            byte[] uncompressedDigest = System.Security.Cryptography.SHA256.HashData(decompressed);
            Assert.AreEqual(DigestUtils.FormatSha256Digest(Convert.ToHexStringLower(uncompressedDigest)), layer.Descriptor.UncompressedDigest);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    // ContentStore.ArtifactRoot is process-wide; other content-store users do not share a resource lock.
    [DoNotParallelize]
    public void Failed_layer_creation_removes_temporary_content()
    {
        string originalRoot = ContentStore.ArtifactRoot;
        string directory = Directory.CreateTempSubdirectory().FullName;
        string source = Path.Combine(directory, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "app"), "payload");
        try
        {
            ContentStore.ArtifactRoot = Path.Combine(directory, "cache");
            Assert.ThrowsExactly<ArgumentException>(() => Layer.FromDirectory(source, "/app", false, "unsupported"));
            Assert.IsEmpty(Directory.GetFiles(ContentStore.ContentRoot));
        }
        finally
        {
            ContentStore.ArtifactRoot = originalRoot;
            Directory.Delete(directory, recursive: true);
        }
    }

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
