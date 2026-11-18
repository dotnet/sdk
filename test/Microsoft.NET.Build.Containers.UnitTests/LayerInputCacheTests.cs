// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class LayerInputCacheTests
{
    public TestContext TestContext { get; }
    public LayerInputCacheTests(TestContext testContext) => TestContext = testContext;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Unchanged_deterministic_layer_is_verified_and_reused(bool windowsLayer)
    {
        WithDirectory((source, store) =>
        {
            string containerPath = windowsLayer ? "C:\\app" : "/app";
            Layer first = Create(source, store, containerPath: containerPath, windowsLayer: windowsLayer);
            DateTime sentinel = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(first.BackingFile, sentinel);
            Layer second = Create(source, store, containerPath: containerPath, windowsLayer: windowsLayer);
            Assert.AreEqual(first.Descriptor, second.Descriptor);
            Assert.AreEqual(sentinel, File.GetLastWriteTimeUtc(second.BackingFile));
            Layer uncached = Create(source, store, containerPath: containerPath, windowsLayer: windowsLayer, enabled: false);
            Assert.AreEqual(first.Descriptor, uncached.Descriptor);
            Assert.AreNotEqual(sentinel, File.GetLastWriteTimeUtc(uncached.BackingFile));
        });
    }

    [TestMethod]
    [DataRow("content")]
    [DataRow("path")]
    [DataRow("timestamp")]
    [DataRow("user")]
    [DataRow("entry-name")]
    public void Changes_to_layer_inputs_invalidate_reuse(string change)
    {
        WithDirectory((source, store) =>
        {
            Layer original = Create(source, store);
            if (change == "content") File.AppendAllText(Path.Combine(source, "payload"), "changed");
            if (change == "entry-name") File.Move(Path.Combine(source, "payload"), Path.Combine(source, "renamed"));
            Layer changed = Create(source, store, containerPath: change == "path" ? "/different" : "/app",
                userId: change == "user" ? 1001 : null, epoch: change == "timestamp" ? 1700000001 : 1700000000);
            Assert.AreNotEqual(original.Descriptor.Digest, changed.Descriptor.Digest);
            Assert.HasCount(2, Directory.GetFiles(store, "layer-input-*.json"));
        });
    }

    [TestMethod]
    [DataRow("compressed-bytes")]
    [DataRow("diff-id")]
    [DataRow("media-type")]
    [DataRow("size")]
    [DataRow("json")]
    [DataRow("missing")]
    public void Corrupted_or_missing_cached_content_is_rebuilt(string corruption)
    {
        WithDirectory((source, store) =>
        {
            Layer original = Create(source, store);
            string metadata = Directory.GetFiles(store, "layer-input-*.json").Single();
            JsonObject entry = JsonNode.Parse(File.ReadAllText(metadata))!.AsObject();
            DateTime sentinel = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(original.BackingFile, sentinel);
            switch (corruption)
            {
                case "compressed-bytes": File.WriteAllBytes(original.BackingFile, new byte[original.Descriptor.Size]); break;
                case "diff-id": entry["UncompressedDigest"] = "sha256:" + new string('a', 64); break;
                case "media-type": entry["MediaType"] = SchemaTypes.DockerLayerGzip; break;
                case "size": entry["Size"] = 1; break;
                case "missing": File.Delete(original.BackingFile); break;
            }
            File.WriteAllText(metadata, corruption == "json" ? "invalid" : entry.ToJsonString());
            Layer restored = Create(source, store);
            Assert.AreEqual(original.Descriptor, restored.Descriptor);
            Assert.AreNotEqual(sentinel, File.GetLastWriteTimeUtc(restored.BackingFile));
            Assert.IsEmpty(Directory.GetFiles(store, "*.tmp"));
        });
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Unix_backslash_names_and_nested_paths_have_distinct_cache_keys()
    {
        WithDirectory((source, store) =>
        {
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            File.Move(Path.Combine(source, "payload"), Path.Combine(source, "nested\\file"));
            Layer literalBackslash = Create(source, store);
            File.Move(Path.Combine(source, "nested\\file"), Path.Combine(source, "nested", "file"));
            Layer nested = Create(source, store);
            Assert.AreNotEqual(literalBackslash.Descriptor.Digest, nested.Descriptor.Digest);
            Assert.HasCount(2, Directory.GetFiles(store, "layer-input-*.json"));
        });
    }

    [TestMethod]
    public async Task Concurrent_publishers_share_valid_layer_and_atomic_metadata()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        string store = Directory.CreateDirectory(Path.Combine(root, "store")).FullName;
        await File.WriteAllTextAsync(Path.Combine(source, "payload"), "shared input", TestContext.CancellationToken);
        try
        {
            Layer[] layers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Create(source, store), TestContext.CancellationToken)));
            Assert.ContainsOnly(layer => layer.Descriptor == layers[0].Descriptor, layers);
            Assert.HasCount(1, Directory.GetFiles(store, "layer-input-*.json"));
            Assert.IsEmpty(Directory.GetFiles(store, "*.tmp"));
            Assert.AreEqual(layers[0].Descriptor, Create(source, store).Descriptor);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Unix_executable_permission_changes_invalidate_layer_reuse()
    {
        WithDirectory((source, store) =>
        {
            Layer first = Create(source, store);
            if (!OperatingSystem.IsWindows())
            {
                string file = Path.Combine(source, "payload");
                File.SetUnixFileMode(file, File.GetUnixFileMode(file) | UnixFileMode.UserExecute);
            }
            Layer changed = Create(source, store);
            Assert.AreNotEqual(first.Descriptor.Digest, changed.Descriptor.Digest);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Metadata_from_another_input_snapshot_cannot_be_reused(bool replaceFingerprint)
    {
        WithDirectory((source, store) =>
        {
            string payload = Path.Combine(source, "payload");
            byte[] originalBytes = File.ReadAllBytes(payload);
            Layer original = Create(source, store);
            string originalMetadata = Directory.GetFiles(store, "layer-input-*.json").Single();
            File.WriteAllText(payload, "different input");
            Create(source, store);
            string otherMetadata = Directory.GetFiles(store, "layer-input-*.json").Single(path => path != originalMetadata);
            string originalFingerprint = JsonNode.Parse(File.ReadAllText(originalMetadata))!["Fingerprint"]!.GetValue<string>();
            JsonNode replacement = JsonNode.Parse(File.ReadAllText(otherMetadata))!;
            if (replaceFingerprint) replacement["Fingerprint"] = originalFingerprint;
            File.WriteAllText(originalMetadata, replacement.ToJsonString());
            File.WriteAllBytes(payload, originalBytes);
            DateTime sentinel = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(original.BackingFile, sentinel);
            Layer restored = Create(source, store);
            Assert.AreEqual(original.Descriptor, restored.Descriptor);
            Assert.AreNotEqual(sentinel, File.GetLastWriteTimeUtc(restored.BackingFile));
        });
    }

    [TestMethod]
    public void Same_length_and_timestamp_cannot_hide_changed_source_bytes()
    {
        WithDirectory((source, store) =>
        {
            string path = Path.Combine(source, "payload");
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            Layer original = Create(source, store);
            byte[] bytes = File.ReadAllBytes(path);
            bytes[1024] ^= 0xff;
            File.WriteAllBytes(path, bytes);
            File.SetLastWriteTimeUtc(path, stamp);
            Layer changed = Create(source, store);
            Assert.AreNotEqual(original.Descriptor.Digest, changed.Descriptor.Digest);
            Assert.AreEqual(changed.Descriptor, Create(source, store, enabled: false).Descriptor);
        });
    }

    [TestMethod]
    public void Touching_unchanged_source_still_reuses_an_epoch_normalized_layer()
    {
        WithDirectory((source, store) =>
        {
            Layer first = Create(source, store);
            DateTime sentinel = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(first.BackingFile, sentinel);
            File.SetLastWriteTimeUtc(Path.Combine(source, "payload"), sentinel.AddYears(20));
            Layer second = Create(source, store);
            Assert.AreEqual(first.Descriptor, second.Descriptor);
            Assert.AreEqual(sentinel, File.GetLastWriteTimeUtc(second.BackingFile));
        });
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("oversized")]
    [DataRow("invalid")]
    [DataRow("unknown-reference")]
    public void Missing_or_corrupt_rejection_hints_do_not_change_image_content(string corruption)
    {
        WithDirectory((source, store) =>
        {
            Layer first = Create(source, store);
            string hint = Directory.GetFiles(store, "*.hint").Single();
            switch (corruption)
            {
                case "missing": File.Delete(hint); break;
                case "oversized": File.WriteAllText(hint, new string('f', 65536)); break;
                case "invalid": File.WriteAllText(hint, new string('.', 64)); break;
                case "unknown-reference": File.WriteAllText(hint, new string('f', 64)); break;
            }
            Layer second = Create(source, store);
            Assert.AreEqual(first.Descriptor, second.Descriptor);
            Assert.IsEmpty(Directory.GetFiles(store, "*.tmp"));
        });
    }

    private Layer Create(string source, string store, string containerPath = "/app", bool windowsLayer = false,
        int? userId = null, long epoch = 1700000000, bool enabled = true)
        => Layer.FromDirectory(source, containerPath, windowsLayer, SchemaTypes.OciManifestV1, userId,
            DateTimeOffset.FromUnixTimeSeconds(epoch), store, TestContext.CancellationToken, enableInputCache: enabled);

    private static void WithDirectory(Action<string, string> test)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        string store = Directory.CreateDirectory(Path.Combine(root, "store")).FullName;
        byte[] bytes = new byte[128 * 1024];
        new Random(1234).NextBytes(bytes);
        File.WriteAllBytes(Path.Combine(source, "payload"), bytes);
        try { test(source, store); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
