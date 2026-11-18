// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[TestClass]
public class ApplicationLayerIncrementalTests : SdkTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Deterministic_layer_reuse_still_resolves_mutable_bases_and_can_be_disabled(bool useMultithreading)
    {
        await using EmptyBaseRegistry registry = new();
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld", identifier: useMultithreading.ToString()).WithSource();
        string archive = Path.Combine(asset.TestRoot, "image.tar");
        string cache = Path.Combine(asset.TestRoot, "cache");
        string[] arguments = [
            "publish", "-t:PublishContainer", "-r", "linux-x64", "-v:normal", $"-mt:{useMultithreading}",
            "-p:EnableSdkContainerSupport=true", "-p:UseAppHost=false", "-p:SelfContained=false",
            $"-p:ContainerBaseImage={registry.Address}/empty:latest",
            "-p:ContainerRepository=layer-cache-test", "-p:ContainerImageTag=test",
            "-p:ContainerArchiveNoIncremental=true", $"-p:ContainerArchiveOutputPath={archive}"
        ];
        Publish(arguments).Should().Pass();
        string store = Path.Combine(OperatingSystem.IsMacOS() ? Path.Combine(cache, "Library", "Caches") : cache,
            "dotnet", "Containers", "Content");
        string applicationLayer = Directory.GetFiles(store, "*.tar.gz").Single();
        Assert.HasCount(1, Directory.GetFiles(store, "layer-input-*.json"));
        DateTime sentinel = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(applicationLayer, sentinel);
        string[] originalContent = ReadArchiveContent();

        // The image is rebuilt, so this exercises layer reuse rather than an archive hit.
        var unchanged = Publish(arguments);
        unchanged.Should().Pass();
        Assert.Contains("Building image", unchanged.StdOut!);
        Assert.AreEqual(sentinel, File.GetLastWriteTimeUtc(applicationLayer));
        Assert.AreSequenceEqual(originalContent, ReadArchiveContent());

        registry.ChangeBaseManifests("changed");
        Publish(arguments).Should().Pass();
        Assert.AreEqual(3, registry.ConfigurationRequests);
        Assert.AreEqual(sentinel, File.GetLastWriteTimeUtc(applicationLayer));

        Publish([.. arguments, "-p:ContainerLayerNoIncremental=true"]).Should().Pass();
        Assert.AreNotEqual(sentinel, File.GetLastWriteTimeUtc(applicationLayer));
        Assert.AreEqual(4, registry.ConfigurationRequests);

        registry.ChangeBaseManifests("new-config", changeConfiguration: true);
        Publish(arguments).Should().Pass();
        Assert.AreEqual(5, registry.ConfigurationRequests);
        Assert.Contains("CONFIG_VARIANT=new-config", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(archive, TestContext.CancellationToken)));
        Assert.IsEmpty(Directory.GetFiles(store, "*.tmp"));

        string[] ReadArchiveContent()
        {
            using FileStream stream = File.OpenRead(archive);
            using TarReader reader = new(stream);
            List<string> content = [];
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.DataStream is { } data)
                {
                    content.Add(entry.Name + ":" + Convert.ToHexString(SHA256.HashData(data)));
                }
            }
            return content.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        Microsoft.DotNet.Cli.Utils.CommandResult Publish(string[] args)
            => new DotnetCommand(Log, args)
                .WithWorkingDirectory(asset.TestRoot)
                .WithEnvironmentVariable("DOTNET_CONTAINER_INSECURE_REGISTRIES", registry.Address)
                .WithEnvironmentVariable("SOURCE_DATE_EPOCH", "1700000000")
                .WithEnvironmentVariable("XDG_CACHE_HOME", cache)
                .WithEnvironmentVariable("LOCALAPPDATA", cache)
                .WithEnvironmentVariable("HOME", cache)
                .Execute();
    }

    private sealed class EmptyBaseRegistry : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentDictionary<string, (string MediaType, byte[] Bytes)> _responses = new();
        private readonly List<Task> _connections = [];
        private readonly Task _server;
        private int _configurationRequests;
        private int _layerRequests;
        private readonly bool _includeLayer;
        internal string? LayerDigest { get; private set; }
        internal int LayerRequests => _layerRequests;

        internal int ConfigurationRequests => _configurationRequests;
        internal string Address { get; }

        internal EmptyBaseRegistry(bool includeLayer = false)
        {
            _includeLayer = includeLayer;
            ChangeBaseManifests("first");
            _listener.Start();
            Address = $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _server = ServeAsync();
        }

        internal void ChangeBaseManifests(string variant, bool changeConfiguration = false)
        {
            List<object> manifests = [];
            object[] layers = [];
            string[] diffIds = [];
            if (_includeLayer)
            {
                byte[] tar = new byte[1024];
                using MemoryStream compressed = new();
                using (GZipStream gzip = new(compressed, CompressionMode.Compress, leaveOpen: true)) gzip.Write(tar);
                byte[] bytes = compressed.ToArray();
                LayerDigest = DigestUtils.FormatSha256Digest(Convert.ToHexStringLower(SHA256.HashData(bytes)));
                _responses[$"/v2/empty/blobs/{LayerDigest}"] = (SchemaTypes.OciLayerGzipV1, bytes);
                layers = [new { mediaType = SchemaTypes.OciLayerGzipV1, digest = LayerDigest, size = bytes.Length }];
                diffIds = [DigestUtils.FormatSha256Digest(Convert.ToHexStringLower(SHA256.HashData(tar)))];
            }
            foreach (string architecture in new[] { "amd64", "arm64" })
            {
                var config = Add(new { architecture, os = "linux", config = changeConfiguration ? new Dictionary<string, object> { ["Env"] = new[] { "CONFIG_VARIANT=" + variant } } : new Dictionary<string, object>(), rootfs = new { type = "layers", diff_ids = diffIds } }, SchemaTypes.OciImageConfigV1, "blobs");
                var manifest = Add(new { schemaVersion = 2, mediaType = SchemaTypes.OciManifestV1, config = new { config.mediaType, config.digest, config.size }, layers, annotations = new Dictionary<string, string> { ["test.variant"] = variant } }, SchemaTypes.OciManifestV1, "manifests");
                manifests.Add(new { manifest.mediaType, manifest.digest, manifest.size, platform = new { architecture, os = "linux" } });
            }
            byte[] index = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, mediaType = SchemaTypes.OciImageIndexV1, manifests });
            _responses["/v2/empty/manifests/latest"] = (SchemaTypes.OciImageIndexV1, index);
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
                while (!_stop.IsCancellationRequested)
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
                byte[] firstByte = new byte[1];
                if (await stream.ReadAsync(firstByte, _stop.Token) != 1)
                {
                    return;
                }
                // The registry client tries HTTPS first, then uses HTTP for an explicitly
                // insecure registry. Close TLS probes promptly rather than awaiting an HTTP line.
                if (firstByte[0] != (byte)'G')
                {
                    return;
                }
                using StreamReader reader = new(stream, leaveOpen: true);
                string? line = await reader.ReadLineAsync(_stop.Token);
                string path = line!.Split(' ')[1];
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_stop.Token))) { }
                bool found = _responses.TryGetValue(path, out var response);
                if (found && response.MediaType == SchemaTypes.OciImageConfigV1)
                {
                    Interlocked.Increment(ref _configurationRequests);
                }
                if (found && path == $"/v2/empty/blobs/{LayerDigest}")
                {
                    Interlocked.Increment(ref _layerRequests);
                    await Task.Delay(TimeSpan.FromSeconds(2), _stop.Token);
                }
                byte[] bytes = found ? response.Bytes : [];
                string headers = $"HTTP/1.1 {(found ? "200 OK" : "404 Not Found")}\r\nContent-Type: {(found ? response.MediaType : "application/json")}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), _stop.Token);
                await stream.WriteAsync(bytes, _stop.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            await _server;
            try
            {
                await Task.WhenAll(_connections);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            _stop.Dispose();
        }
    }
}
