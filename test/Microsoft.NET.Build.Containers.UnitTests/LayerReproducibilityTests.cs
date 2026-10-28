// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class LayerReproducibilityTests
{
    private const string ManifestMediaType = "application/vnd.docker.distribution.manifest.v2+json";
    private static readonly DateTimeOffset ReproducibleTimestamp = DateTimeOffset.FromUnixTimeSeconds(1636374896);

    private string CreateContentDirectory()
    {
        string directory = Path.Combine(TestContext.ResultsDirectory!, Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(directory, "subdirectory"));
        File.WriteAllText(Path.Combine(directory, "app.dll"), $"some content for {TestContext.TestName}");
        File.WriteAllText(Path.Combine(directory, "subdirectory", "app.deps.json"), $"some other content for {TestContext.TestName}");
        return directory;
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LayersBuiltFromIdenticalContentHaveTheSameDigest()
    {
        // This is the behavior the change exists for: publishing the same content twice should produce
        // the same layer, so a rebuild does not appear to downstream tooling as a new artifact.
        string first = CreateContentDirectory();
        string second = CreateContentDirectory();
        File.SetLastWriteTimeUtc(Path.Combine(second, "app.dll"), new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));

        Layer firstLayer = Layer.FromDirectory(first, "/app", false, ManifestMediaType, userId: null, modificationTime: ReproducibleTimestamp);
        Layer secondLayer = Layer.FromDirectory(second, "/app", false, ManifestMediaType, userId: null, modificationTime: ReproducibleTimestamp);

        Assert.AreEqual(firstLayer.Descriptor.Digest, secondLayer.Descriptor.Digest);
        Assert.AreEqual(firstLayer.Descriptor.Size, secondLayer.Descriptor.Size);

        // The layer must not depend on the process that produced it, which the process id in the
        // pax extended header names would otherwise leak in.
        using FileStream compressed = File.OpenRead(firstLayer.BackingFile);
        using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
        using var text = new StreamReader(decompressed);
        Assert.IsFalse(
            text.ReadToEnd().Contains($"PaxHeaders.{Environment.ProcessId}", StringComparison.Ordinal),
            "The layer should not contain the current process id.");
    }

    [TestMethod]
    public void LayersBuiltFromDifferentContentHaveDifferentDigests()
    {
        // The digest must still be a function of the content: pinning the timestamp must not make
        // genuinely different inputs collide.
        string first = CreateContentDirectory();
        string second = CreateContentDirectory();
        File.WriteAllText(Path.Combine(second, "app.dll"), "some different content");

        Assert.AreNotEqual(
            Layer.FromDirectory(first, "/app", false, ManifestMediaType, userId: null, modificationTime: ReproducibleTimestamp).Descriptor.Digest,
            Layer.FromDirectory(second, "/app", false, ManifestMediaType, userId: null, modificationTime: ReproducibleTimestamp).Descriptor.Digest);
    }

    [TestMethod]
    public void EveryLayerEntryCarriesTheTimestampFromSourceDateEpoch()
    {
        Layer layer = Layer.FromDirectory(
            CreateContentDirectory(),
            "/app",
            false,
            ManifestMediaType,
            userId: null,
            modificationTime: ReproducibleTimestamp);

        using FileStream compressed = File.OpenRead(layer.BackingFile);
        using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new TarReader(decompressed);

        int entries = 0;
        while (reader.GetNextEntry() is TarEntry entry)
        {
            entries++;
            Assert.AreEqual(ReproducibleTimestamp, entry.ModificationTime, $"Entry '{entry.Name}' has an unexpected timestamp.");
        }

        Assert.AreEqual(4, entries, "Expected the app directory, the subdirectory and the two files.");
    }

    [TestMethod]
    public void LayerPreservesFileContentThatLooksLikeAPaxHeader()
    {
        byte[] expected = new byte[512];
        expected.AsSpan().Fill((byte)'a');
        "ustar"u8.CopyTo(expected.AsSpan(257));
        expected[156] = (byte)'x';
        "./PaxHeaders.99999/."u8.CopyTo(expected);

        string directory = CreateContentDirectory();
        File.WriteAllBytes(Path.Combine(directory, "app.dll"), expected);
        Layer layer = Layer.FromDirectory(
            directory,
            "/app",
            false,
            ManifestMediaType,
            userId: null,
            modificationTime: ReproducibleTimestamp);

        using FileStream compressed = File.OpenRead(layer.BackingFile);
        using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new TarReader(decompressed);

        while (reader.GetNextEntry() is TarEntry entry)
        {
            if (entry.Name == "app/app.dll")
            {
                using var actual = new MemoryStream();
                entry.DataStream!.CopyTo(actual);
                Assert.AreSequenceEqual(expected, actual.ToArray());
                return;
            }
        }

        Assert.Fail("The layer did not contain app/app.dll.");
    }
}
