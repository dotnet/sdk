// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.Text.Json;
using Microsoft.NET.Build.Containers.LocalDaemons;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ContainerArchiveTests
{
    [TestMethod]
    public async Task Rejects_unsupported_manifest_media_type()
    {
        BuiltImage image = new()
        {
            Config = string.Empty,
            Manifest = string.Empty,
            ManifestDigest = string.Empty,
            ManifestMediaType = "unsupported",
            Architecture = "unknown",
            OS = "unknown"
        };

        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => ContainerArchive.WriteImageToStreamAsync(image, default, default, Stream.Null, default));
    }

    [TestMethod]
    [DataRow(SchemaTypes.DockerManifestV2)]
    [DataRow(SchemaTypes.OciManifestV1)]
    public async Task Requires_image_sha(string manifestMediaType)
    {
        BuiltImage image = new()
        {
            Config = string.Empty,
            Manifest = string.Empty,
            ManifestDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000",
            ManifestMediaType = manifestMediaType,
            Layers = [],
            Architecture = "unknown",
            OS = "unknown"
        };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ContainerArchive.WriteImageToStreamAsync(image, default, default, Stream.Null, default));
        Assert.Contains("image SHA", exception.Message);
    }

    [TestMethod]
    public async Task Oci_layout_supports_docker_manifest()
    {
        const string config = "{}";
        string imageSha = DigestUtils.ComputeSha256(config);
        string imageDigest = DigestUtils.FormatSha256Digest(imageSha);
        string manifest =
            $$"""
            {
              "schemaVersion": 2,
              "mediaType": "{{SchemaTypes.DockerManifestV2}}",
              "config": {
                "mediaType": "{{SchemaTypes.DockerContainerV1}}",
                "size": 2,
                "digest": "{{imageDigest}}"
              },
              "layers": []
            }
            """;
        string manifestDigest = DigestUtils.ComputeSha256Digest(manifest);
        BuiltImage image = new()
        {
            Config = config,
            ImageDigest = imageDigest,
            ImageSha = imageSha,
            Manifest = manifest,
            ManifestDigest = manifestDigest,
            ManifestMediaType = SchemaTypes.DockerManifestV2,
            Layers = [],
            Architecture = "arm64",
            OS = "linux"
        };
        DestinationImageReference destination = new(
            new ArchiveFileRegistry("unused"),
            "repository",
            ["tag"]);
        using MemoryStream archive = new();

        await ContainerArchive.WriteOciImageToStreamAsync(
            image,
            default,
            destination,
            archive,
            TestContext.CancellationToken);

        archive.Position = 0;
        using TarReader reader = new(archive);
        var entries = new Dictionary<string, string>();
        while (await reader.GetNextEntryAsync(copyData: false, cancellationToken: TestContext.CancellationToken) is { } entry)
        {
            if (entry.DataStream is not null)
            {
                using StreamReader streamReader = new(entry.DataStream);
                entries.Add(entry.Name, await streamReader.ReadToEndAsync(TestContext.CancellationToken));
            }
        }

        Assert.Contains("oci-layout", entries.Keys);
        string manifestPath = $"blobs/sha256/{DigestUtils.GetEncoded(manifestDigest)}";
        Assert.AreEqual(manifest, entries[manifestPath]);
        using JsonDocument index = JsonDocument.Parse(entries["index.json"]);
        Assert.AreEqual(SchemaTypes.OciImageIndexV1, index.RootElement.GetProperty("mediaType").GetString());
        Assert.AreEqual(
            SchemaTypes.DockerManifestV2,
            index.RootElement.GetProperty("manifests")[0].GetProperty("mediaType").GetString());
        Assert.AreEqual(
            "arm64",
            index.RootElement.GetProperty("manifests")[0].GetProperty("platform").GetProperty("architecture").GetString());
    }

    [TestMethod]
    public async Task Multi_arch_oci_layout_has_unknown_platform_for_the_image_index_descriptor()
    {
        BuiltImage[] images =
        [
            CreateImage("amd64"),
            CreateImage("arm64")
        ];
        (string imageIndex, string imageIndexMediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        MultiArchImage multiArchImage = new()
        {
            ImageIndex = imageIndex,
            ImageIndexMediaType = imageIndexMediaType,
            Images = images
        };
        DestinationImageReference destination = new(
            new ArchiveFileRegistry("unused"),
            "repository",
            ["tag"]);
        using MemoryStream archive = new();

        await ContainerArchive.WriteMultiArchOciImageToStreamAsync(
            multiArchImage,
            default,
            destination,
            archive,
            TestContext.CancellationToken);

        archive.Position = 0;
        using TarReader reader = new(archive);
        var entries = new Dictionary<string, string>();
        while (await reader.GetNextEntryAsync(copyData: false, cancellationToken: TestContext.CancellationToken) is { } entry)
        {
            if (entry.DataStream is not null)
            {
                using StreamReader streamReader = new(entry.DataStream);
                entries.Add(entry.Name, await streamReader.ReadToEndAsync(TestContext.CancellationToken));
            }
        }

        using JsonDocument index = JsonDocument.Parse(entries["index.json"]);
        JsonElement manifests = index.RootElement.GetProperty("manifests");
        Assert.AreEqual(1, manifests.GetArrayLength());
        Assert.AreEqual(SchemaTypes.DockerManifestListV2, manifests[0].GetProperty("mediaType").GetString());
        Assert.AreEqual("unknown", manifests[0].GetProperty("platform").GetProperty("architecture").GetString());
        Assert.AreEqual("unknown", manifests[0].GetProperty("platform").GetProperty("os").GetString());
        string imageIndexPath = $"blobs/sha256/{DigestUtils.GetEncoded(DigestUtils.ComputeSha256Digest(imageIndex))}";
        Assert.AreEqual(imageIndex, entries[imageIndexPath]);
        Assert.IsTrue(entries.ContainsKey($"blobs/sha256/{DigestUtils.GetEncoded(images[0].ManifestDigest)}"));
        Assert.IsTrue(entries.ContainsKey($"blobs/sha256/{DigestUtils.GetEncoded(images[1].ManifestDigest)}"));
    }

    private static BuiltImage CreateImage(string architecture)
    {
        string config = $$"""{"architecture":"{{architecture}}","os":"linux"}""";
        string imageSha = DigestUtils.ComputeSha256(config);
        string imageDigest = DigestUtils.FormatSha256Digest(imageSha);
        string manifest =
            $$"""
            {
              "schemaVersion": 2,
              "mediaType": "{{SchemaTypes.DockerManifestV2}}",
              "config": {
                "mediaType": "{{SchemaTypes.DockerContainerV1}}",
                "size": {{config.Length}},
                "digest": "{{imageDigest}}"
              },
              "layers": []
            }
            """;

        return new BuiltImage
        {
            Config = config,
            ImageDigest = imageDigest,
            ImageSha = imageSha,
            Manifest = manifest,
            ManifestDigest = DigestUtils.ComputeSha256Digest(manifest),
            ManifestMediaType = SchemaTypes.DockerManifestV2,
            Layers = [],
            Architecture = architecture,
            OS = "linux"
        };
    }

    public TestContext TestContext { get; set; } = default!;
}
