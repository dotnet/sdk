// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class LayerReproducibilityTests
{
    private const string ManifestMediaType = "application/vnd.docker.distribution.manifest.v2+json";

    private string CreateContentDirectory()
    {
        string directory = Path.Combine(TestContext.ResultsDirectory!, Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(directory, "subdirectory"));
        File.WriteAllText(Path.Combine(directory, "app.dll"), "some content");
        File.WriteAllText(Path.Combine(directory, "subdirectory", "app.deps.json"), "some other content");
        return directory;
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void LayersBuiltFromIdenticalContentHaveTheSameDigest()
    {
        // This is the behavior the change exists for: publishing the same content twice should produce
        // the same layer, so a rebuild does not appear to downstream tooling as a new artifact.
        string first = CreateContentDirectory();
        string second = CreateContentDirectory();
        File.SetLastWriteTimeUtc(Path.Combine(second, "app.dll"), new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));

        Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", "1636374896");
        try
        {
            Layer firstLayer = Layer.FromDirectory(first, "/app", false, ManifestMediaType);
            Layer secondLayer = Layer.FromDirectory(second, "/app", false, ManifestMediaType);

            Assert.AreEqual(firstLayer.Descriptor.Digest, secondLayer.Descriptor.Digest);
            Assert.AreEqual(firstLayer.Descriptor.Size, secondLayer.Descriptor.Size);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", null);
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void LayersBuiltFromDifferentContentHaveDifferentDigests()
    {
        // The digest must still be a function of the content: pinning the timestamp must not make
        // genuinely different inputs collide.
        string first = CreateContentDirectory();
        string second = CreateContentDirectory();
        File.WriteAllText(Path.Combine(second, "app.dll"), "some different content");

        Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", "1636374896");
        try
        {
            Assert.AreNotEqual(
                Layer.FromDirectory(first, "/app", false, ManifestMediaType).Descriptor.Digest,
                Layer.FromDirectory(second, "/app", false, ManifestMediaType).Descriptor.Digest);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", null);
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void EveryLayerEntryCarriesTheTimestampFromSourceDateEpoch()
    {
        var expected = new DateTimeOffset(2021, 11, 8, 12, 34, 56, TimeSpan.Zero);

        Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", "1636374896");
        Layer layer;
        try
        {
            layer = Layer.FromDirectory(CreateContentDirectory(), "/app", false, ManifestMediaType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", null);
        }

        using FileStream compressed = File.OpenRead(layer.BackingFile);
        using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new TarReader(decompressed);

        int entries = 0;
        while (reader.GetNextEntry() is TarEntry entry)
        {
            entries++;
            Assert.AreEqual(expected, entry.ModificationTime, $"Entry '{entry.Name}' has an unexpected timestamp.");
        }

        Assert.AreEqual(4, entries, "Expected the app directory, the subdirectory and the two files.");
    }
}
