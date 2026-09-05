// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public sealed class LocalImageSourceTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DataRow("docker.io/library/custom-base:latest", "localhost/custom-base:latest")]
    [DataRow("docker.io/example/custom-base@sha256:1234", "localhost/example/custom-base@sha256:1234")]
    [DataRow("mcr.microsoft.com/dotnet/runtime:10.0", null)]
    public void GetsPodmanLocalReference(string imageReference, string? expected)
        => Assert.AreEqual(expected, LocalImageSource.GetPodmanLocalReference(imageReference));

    [TestMethod]
    public async Task ReadsDockerArchiveAndMakesCompressedLayersAvailable()
    {
        const string imageReference = "example/base:latest";
        await using MemoryStream archive = await CreateDockerArchiveAsync(imageReference);
        using TestLoggerFactory loggerFactory = new(TestContext);

        string blobPath;
        await using (LocalImageSource source = await LocalImageSource.CreateFromArchiveAsync(
            imageReference,
            archive,
            loggerFactory,
            TestContext.CancellationToken))
        {
            ImageBuilder builder = await source.GetImageManifestAsync(
                "example/base",
                "latest",
                "linux-x64",
                new ExactRidManifestPicker(),
                TestContext.CancellationToken);
            BuiltImage image = builder.Build();

            Assert.AreEqual("linux", image.OS);
            Assert.AreEqual("amd64", image.Architecture);
            Descriptor layer = image.LayerDescriptors.Single();
            Assert.AreEqual(SchemaTypes.DockerLayerGzip, layer.MediaType);

            blobPath = await source.GetBlobPathAsync("example/base", layer, TestContext.CancellationToken);
            Assert.IsTrue(File.Exists(blobPath));
            await using FileStream blob = File.OpenRead(blobPath);
            Assert.AreEqual(0x1f, blob.ReadByte());
            Assert.AreEqual(0x8b, blob.ReadByte());
        }

        Assert.IsFalse(File.Exists(blobPath));
    }

    [TestMethod]
    public async Task SelectsPlatformFromNestedOciImageIndex()
    {
        const string imageReference = "example/base:latest";
        (MemoryStream archive, string expectedLayerDigest) = await CreateOciArchiveAsync(imageReference);
        await using (archive)
        using (TestLoggerFactory loggerFactory = new(TestContext))
        await using (LocalImageSource source = await LocalImageSource.CreateFromArchiveAsync(
            imageReference,
            archive,
            loggerFactory,
            TestContext.CancellationToken))
        {
            ImageBuilder builder = await source.GetImageManifestAsync(
                "example/base",
                "latest",
                "linux-x64",
                new ExactRidManifestPicker(),
                TestContext.CancellationToken);
            BuiltImage image = builder.Build();

            Assert.AreEqual("amd64", image.Architecture);
            Descriptor layer = image.LayerDescriptors.Single();
            Assert.AreEqual(expectedLayerDigest, layer.Digest);
            Assert.IsTrue(File.Exists(await source.GetBlobPathAsync(
                "example/base",
                layer,
                TestContext.CancellationToken)));
        }
    }

    private async Task<MemoryStream> CreateDockerArchiveAsync(string imageReference)
    {
        byte[] layer = await CreateLayerAsync();
        const string config = """
            {
              "architecture": "amd64",
              "config": {},
              "os": "linux",
              "rootfs": { "type": "layers", "diff_ids": [] }
            }
            """;
        string manifest = $$"""
            [{
              "Config": "config.json",
              "RepoTags": ["{{imageReference}}"],
              "Layers": ["layer/layer.tar"]
            }]
            """;

        MemoryStream archive = new();
        using (TarWriter writer = new(archive, TarEntryFormat.Pax, leaveOpen: true))
        {
            await WriteEntryAsync(writer, "config.json", Encoding.UTF8.GetBytes(config));
            await WriteEntryAsync(writer, "layer/layer.tar", layer);
            await WriteEntryAsync(writer, "manifest.json", Encoding.UTF8.GetBytes(manifest));
        }
        archive.Position = 0;
        return archive;
    }

    private async Task<(MemoryStream Archive, string Amd64LayerDigest)> CreateOciArchiveAsync(string imageReference)
    {
        var blobs = new Dictionary<string, byte[]>();
        var manifestDescriptors = new List<string>();
        string amd64LayerDigest = string.Empty;

        foreach ((string architecture, string rid) in new[] { ("amd64", "x64"), ("arm64", "arm64") })
        {
            byte[] config = Encoding.UTF8.GetBytes($$"""
                {
                  "architecture": "{{architecture}}",
                  "config": {},
                  "os": "linux",
                  "rootfs": {
                    "type": "layers",
                    "diff_ids": []
                  }
                }
                """);
            string configDigest = AddBlob(blobs, config);
            byte[] layer = Encoding.UTF8.GetBytes($"{architecture} layer");
            string layerDigest = AddBlob(blobs, layer);
            if (rid == "x64")
            {
                amd64LayerDigest = layerDigest;
            }

            byte[] manifest = Encoding.UTF8.GetBytes($$"""
                {
                  "schemaVersion": 2,
                  "mediaType": "{{SchemaTypes.OciManifestV1}}",
                  "config": {
                    "mediaType": "{{SchemaTypes.OciImageConfigV1}}",
                    "size": {{config.Length}},
                    "digest": "{{configDigest}}"
                  },
                  "layers": [
                    {
                      "mediaType": "{{SchemaTypes.OciLayerGzipV1}}",
                      "size": {{layer.Length}},
                      "digest": "{{layerDigest}}"
                    }
                  ]
                }
                """);
            string manifestDigest = AddBlob(blobs, manifest);
            manifestDescriptors.Add($$"""
                {
                  "mediaType": "{{SchemaTypes.OciManifestV1}}",
                  "size": {{manifest.Length}},
                  "digest": "{{manifestDigest}}",
                  "platform": {
                    "architecture": "{{architecture}}",
                    "os": "linux"
                  }
                }
                """);
        }

        byte[] nestedIndex = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "mediaType": "{{SchemaTypes.OciImageIndexV1}}",
              "manifests": [
                {{string.Join(',', manifestDescriptors)}}
              ]
            }
            """);
        string nestedIndexDigest = AddBlob(blobs, nestedIndex);
        string rootIndex = $$"""
            {
              "schemaVersion": 2,
              "manifests": [
                {
                  "mediaType": "{{SchemaTypes.OciImageIndexV1}}",
                  "size": {{nestedIndex.Length}},
                  "digest": "{{nestedIndexDigest}}",
                  "annotations": {
                    "io.containerd.image.name": "{{imageReference}}"
                  }
                }
              ]
            }
            """;

        MemoryStream archive = new();
        using (TarWriter writer = new(archive, TarEntryFormat.Pax, leaveOpen: true))
        {
            await WriteEntryAsync(writer, "oci-layout", "{\"imageLayoutVersion\":\"1.0.0\"}"u8.ToArray());
            await WriteEntryAsync(writer, "index.json", Encoding.UTF8.GetBytes(rootIndex));
            foreach ((string digest, byte[] content) in blobs)
            {
                await WriteEntryAsync(writer, $"blobs/sha256/{DigestUtils.GetEncoded(digest)}", content);
            }
        }
        archive.Position = 0;
        return (archive, amd64LayerDigest);
    }

    private static string AddBlob(Dictionary<string, byte[]> blobs, byte[] content)
    {
        string digest = DigestUtils.FormatSha256Digest(Convert.ToHexStringLower(SHA256.HashData(content)));
        blobs.Add(digest, content);
        return digest;
    }

    private async Task<byte[]> CreateLayerAsync()
    {
        using MemoryStream layer = new();
        using (TarWriter writer = new(layer, TarEntryFormat.Pax, leaveOpen: true))
        {
            await WriteEntryAsync(writer, "content.txt", "local base layer"u8.ToArray());
        }
        return layer.ToArray();
    }

    private async Task WriteEntryAsync(TarWriter writer, string name, byte[] content)
    {
        using MemoryStream data = new(content);
        PaxTarEntry entry = new(TarEntryType.RegularFile, name) { DataStream = data };
        await writer.WriteEntryAsync(entry, TestContext.CancellationToken);
    }

    private sealed class ExactRidManifestPicker : IManifestPicker
    {
        public PlatformSpecificManifest? PickBestManifestForRid(
            IReadOnlyDictionary<string, PlatformSpecificManifest> manifestList,
            string runtimeIdentifier)
            => manifestList.GetValueOrDefault(runtimeIdentifier);

        public PlatformSpecificOciManifest? PickBestManifestForRid(
            IReadOnlyDictionary<string, PlatformSpecificOciManifest> manifestList,
            string runtimeIdentifier)
            => manifestList.GetValueOrDefault(runtimeIdentifier);
    }
}
