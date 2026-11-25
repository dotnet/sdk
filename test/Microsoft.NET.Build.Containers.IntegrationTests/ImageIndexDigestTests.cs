// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Formats.Tar;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FakeItEasy;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Tasks;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[TestClass]
public class ImageIndexDigestTests : SdkTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Publish_exposes_digest_of_archived_index_bytes(bool useMultithreading)
    {
        await using EmptyBaseRegistry registry = new();
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld", identifier: useMultithreading.ToString()).WithSource()
            .WithProjectChanges(project => project.Root!.Add(new XElement("PropertyGroup",
                new XElement("RuntimeIdentifiers", "linux-x64;linux-arm64"))));
        string archive = Path.Combine(asset.TestRoot, "image.tar");
        string? previousDigest = null;
        for (int publish = 0; publish < 2; publish++)
        {
            var result = new DotnetCommand(Log, "publish", "-t:PublishContainer", $"-mt:{useMultithreading}",
                "-p:EnableSdkContainerSupport=true", "-p:UseAppHost=false", "-p:SelfContained=false",
                $"-p:ContainerBaseImage={registry.Address}/empty:latest", "-p:ContainerRepository=index-digest-test",
                "-p:ContainerImageTag=test", $"-p:ContainerArchiveOutputPath={archive}",
                "-getProperty:GeneratedImageIndex,GeneratedImageIndexDigest", "-getItem:GeneratedContainer")
                .WithWorkingDirectory(asset.TestRoot)
                .WithEnvironmentVariable("SOURCE_DATE_EPOCH", "1700000000")
                .WithEnvironmentVariable("DOTNET_CONTAINER_INSECURE_REGISTRIES", registry.Address)
                .Execute();
            result.Should().Pass();
            using JsonDocument outputs = JsonDocument.Parse(result.StdOut!);
            JsonElement properties = outputs.RootElement.GetProperty("Properties");
            string index = properties.GetProperty("GeneratedImageIndex").GetString()!;
            string digest = properties.GetProperty("GeneratedImageIndexDigest").GetString()!;
            Dictionary<string, byte[]> entries = [];
            using (TarReader reader = new(File.OpenRead(archive)))
            {
                while (reader.GetNextEntry() is { } entry)
                {
                    if (entry.DataStream is { } data)
                    {
                        using MemoryStream bytes = new();
                        data.CopyTo(bytes);
                        entries.Add(entry.Name, bytes.ToArray());
                    }
                }
            }
            using JsonDocument root = JsonDocument.Parse(entries["index.json"]);
            string archivedDigest = root.RootElement.GetProperty("manifests")[0].GetProperty("digest").GetString()!;
            Assert.AreEqual(archivedDigest, digest);
            byte[] payload = entries["blobs/sha256/" + digest["sha256:".Length..]];
            Assert.AreSequenceEqual(Encoding.UTF8.GetBytes(index), payload);
            Assert.AreEqual("sha256:" + Convert.ToHexStringLower(SHA256.HashData(payload)), digest);
            if (publish == 0)
            {
                TaskItem[] images = outputs.RootElement.GetProperty("Items").GetProperty("GeneratedContainer").EnumerateArray()
                    .DistinctBy(item => item.GetProperty("ManifestDigest").GetString())
                    .Select(item =>
                    {
                        TaskItem image = new(item.GetProperty("Identity").GetString()!);
                        foreach (string metadata in new[] { "Configuration", "Manifest", "ManifestDigest", "ManifestMediaType" })
                        {
                            image.SetMetadata(metadata, item.GetProperty(metadata).GetString()!);
                        }
                        return image;
                    }).ToArray();
                Assert.HasCount(2, images);
                using CreateImageIndex remote = new()
                {
                    BuildEngine = A.Fake<IBuildEngine>(),
                    OutputRegistry = registry.Address,
                    Repository = "index-digest-test",
                    ImageTags = ["first", "second"],
                    GeneratedContainers = images
                };
                Assert.IsTrue(await remote.ExecuteAsync(TestContext.CancellationToken));
                Assert.AreEqual(index, remote.GeneratedImageIndex);
                Assert.AreEqual(digest, remote.GeneratedImageIndexDigest);
                Assert.HasCount(2, registry.UploadedIndexes);
                foreach (byte[] uploaded in registry.UploadedIndexes)
                {
                    Assert.AreSequenceEqual(payload, uploaded);
                }
            }
            if (previousDigest is not null)
            {
                Assert.AreEqual(previousDigest, digest);
            }
            previousDigest = digest;
        }
    }

    // An empty-layer base keeps this target-wiring test independent of Docker and external registries.
    private sealed class EmptyBaseRegistry : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentBag<Task> _connections = [];
        private readonly Dictionary<string, (string MediaType, byte[] Bytes)> _responses = [];
        private readonly Task _server;
        internal string Address { get; }
        internal ConcurrentBag<byte[]> UploadedIndexes { get; } = [];

        internal EmptyBaseRegistry()
        {
            List<object> manifests = [];
            foreach (string architecture in new[] { "amd64", "arm64" })
            {
                var config = Add(new { architecture, os = "linux", config = new Dictionary<string, string>(),
                    rootfs = new { type = "layers", diff_ids = Array.Empty<string>() } }, SchemaTypes.OciImageConfigV1, "blobs");
                var manifest = Add(new { schemaVersion = 2, mediaType = SchemaTypes.OciManifestV1,
                    config = new { config.mediaType, config.digest, config.size },
                    layers = Array.Empty<object>() }, SchemaTypes.OciManifestV1, "manifests");
                manifests.Add(new { manifest.mediaType, manifest.digest, manifest.size, platform = new { architecture, os = "linux" } });
            }
            _responses["/v2/empty/manifests/latest"] = (SchemaTypes.OciImageIndexV1,
                JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, mediaType = SchemaTypes.OciImageIndexV1, manifests }));
            _listener.Start();
            Address = $"localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _server = ServeAsync();
        }

        private (string mediaType, string digest, int size) Add(object value, string mediaType, string kind)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            string digest = DigestUtils.ComputeSha256Digest(Encoding.UTF8.GetString(bytes));
            _responses[$"/v2/empty/{kind}/{digest}"] = (mediaType, bytes);
            return (mediaType, digest, bytes.Length);
        }

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _connections.Add(RespondAsync(client));
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }

        private async Task RespondAsync(TcpClient client)
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                List<byte> header = [];
                byte[] next = new byte[1];
                while (await stream.ReadAsync(next, _stop.Token) != 0)
                {
                    header.Add(next[0]);
                    // Insecure registries are probed with TLS first; close that probe so the SDK retries HTTP.
                    if (header.Count == 1 && next[0] == 0x16)
                    {
                        return;
                    }
                    if (header.Count >= 4 && header[^4] == '\r' && header[^3] == '\n'
                        && header[^2] == '\r' && header[^1] == '\n')
                    {
                        break;
                    }
                }
                if (header.Count == 0)
                {
                    return;
                }
                string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                string[] request = lines[0].Split(' ');
                string path = request[1];
                bool found = _responses.TryGetValue(path, out var response);
                byte[] body = found ? response.Bytes : [];
                string status = found ? "200 OK" : "404 Not Found";
                if (request[0] == "PUT")
                {
                    string length = lines.Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                    byte[] uploaded = new byte[int.Parse(length["Content-Length:".Length..], System.Globalization.CultureInfo.InvariantCulture)];
                    await stream.ReadExactlyAsync(uploaded, _stop.Token);
                    UploadedIndexes.Add(uploaded);
                    status = "201 Created";
                }
                byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {response.MediaType ?? "application/json"}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headers, _stop.Token);
                await stream.WriteAsync(body, _stop.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            await _server;
            try { await Task.WhenAll(_connections); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            _listener.Stop();
            _stop.Dispose();
        }
    }
}
