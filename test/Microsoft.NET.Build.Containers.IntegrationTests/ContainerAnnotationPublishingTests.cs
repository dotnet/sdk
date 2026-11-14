// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.Text.Json;
using System.Xml.Linq;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[TestClass]
public class ContainerAnnotationPublishingTests : SdkTest
{
    [TestMethod]
    public void MultiPlatformArchivePreservesIndexAndChildAnnotations()
    {
        const string source = "https://example.com/source";
        const string manifestValue = "pipeline;value:with%punctuation";
        const string created = "2021-11-08T12:34:56.0000000Z";
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld").WithSource().WithProjectChanges(project =>
        {
            XNamespace ns = project.Root!.Name.Namespace;
            project.Root.Add(new XElement(ns + "PropertyGroup",
                new XElement(ns + "RuntimeIdentifiers", "linux-x64;linux-arm64"),
                new XElement(ns + "EnableSdkContainerSupport", "true"),
                new XElement(ns + "ContainerImageFormat", "OCI"),
                new XElement(ns + "ContainerBaseImage", "mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled"),
                new XElement(ns + "ContainerArchiveOutputPath", "image.tar"),
                new XElement(ns + "ContainerRepository", "annotation-test"),
                new XElement(ns + "PublishRepositoryUrl", "true"),
                new XElement(ns + "PrivateRepositoryUrl", source),
                new XElement(ns + "SourceRevisionId", "abcdef")));
            project.Root.Add(new XElement(ns + "ItemGroup",
                Annotation("example.manifest", manifestValue, "Manifest"),
                Annotation("example.index", "index-only", "Index"),
                Annotation("example.both", "both-scopes", "Manifest,Index")));

            XElement Annotation(string name, string value, string scope) => new(ns + "ContainerAnnotation",
                new XAttribute("Include", name), new XAttribute("Value", value), new XAttribute("Scope", scope));
        });

        new DotnetCommand(Log, "publish", "/t:PublishContainer")
            .WithWorkingDirectory(asset.TestRoot)
            .WithEnvironmentVariable("SOURCE_DATE_EPOCH", "1636374896")
            .Execute()
            .Should().Pass();

        using FileStream archive = File.OpenRead(Path.Combine(asset.TestRoot, "image.tar"));
        using TarReader reader = new(archive);
        Dictionary<string, string> entries = new(StringComparer.Ordinal);
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.DataStream is not null)
            {
                using StreamReader content = new(entry.DataStream);
                entries.Add(entry.Name, content.ReadToEnd());
            }
        }

        using JsonDocument layout = JsonDocument.Parse(entries["index.json"]);
        using JsonDocument index = ReadBlob(layout.RootElement.GetProperty("manifests")[0]);
        Assert.AreEqual(SchemaTypes.OciImageIndexV1, index.RootElement.GetProperty("mediaType").GetString());
        JsonElement indexAnnotations = index.RootElement.GetProperty("annotations");
        AssertCommonAnnotations(indexAnnotations);
        Assert.AreEqual("index-only", indexAnnotations.GetProperty("example.index").GetString());
        Assert.IsFalse(indexAnnotations.TryGetProperty("example.manifest", out _));
        Assert.IsFalse(indexAnnotations.TryGetProperty("org.opencontainers.image.base.digest", out _));
        Assert.IsFalse(indexAnnotations.TryGetProperty("net.dot.sdk.version", out _));

        JsonElement manifests = index.RootElement.GetProperty("manifests");
        Assert.AreEqual(2, manifests.GetArrayLength());
        string?[] architectures = manifests.EnumerateArray().Select(manifest => manifest.GetProperty("platform").GetProperty("architecture").GetString()).ToArray();
        Assert.Contains("amd64", architectures);
        Assert.Contains("arm64", architectures);
        HashSet<string> baseDigests = new(StringComparer.Ordinal);
        foreach (JsonElement descriptor in manifests.EnumerateArray())
        {
            using JsonDocument manifest = ReadBlob(descriptor);
            Assert.AreEqual(SchemaTypes.OciManifestV1, manifest.RootElement.GetProperty("mediaType").GetString());
            JsonElement annotations = manifest.RootElement.GetProperty("annotations");
            AssertCommonAnnotations(annotations);
            Assert.AreEqual(manifestValue, annotations.GetProperty("example.manifest").GetString());
            Assert.IsFalse(annotations.TryGetProperty("example.index", out _));
            Assert.StartsWith("sha256:", annotations.GetProperty("org.opencontainers.image.base.digest").GetString()!);
            Assert.IsFalse(string.IsNullOrEmpty(annotations.GetProperty("net.dot.sdk.version").GetString()));
            baseDigests.Add(annotations.GetProperty("org.opencontainers.image.base.digest").GetString()!);
            using JsonDocument config = ReadBlob(manifest.RootElement.GetProperty("config"));
            JsonElement labels = config.RootElement.GetProperty("config").GetProperty("Labels");
            Assert.AreEqual(source, labels.GetProperty("org.opencontainers.image.source").GetString());
            Assert.AreEqual("abcdef", labels.GetProperty("org.opencontainers.image.revision").GetString());
        }

        Assert.HasCount(2, baseDigests);

        JsonDocument ReadBlob(JsonElement descriptor)
        {
            string digest = descriptor.GetProperty("digest").GetString()!;
            string content = entries["blobs/sha256/" + DigestUtils.GetEncoded(digest)];
            Assert.AreEqual(digest, DigestUtils.ComputeSha256Digest(content));
            return JsonDocument.Parse(content);
        }

        static void AssertCommonAnnotations(JsonElement annotations)
        {
            Assert.AreEqual(source, annotations.GetProperty("org.opencontainers.image.source").GetString());
            Assert.AreEqual("abcdef", annotations.GetProperty("org.opencontainers.image.revision").GetString());
            Assert.AreEqual(created, annotations.GetProperty("org.opencontainers.image.created").GetString());
            Assert.AreEqual("both-scopes", annotations.GetProperty("example.both").GetString());
        }
    }
}
