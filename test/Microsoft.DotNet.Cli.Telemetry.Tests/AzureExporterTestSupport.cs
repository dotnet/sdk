// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public static class ExporterProcessConfiguration
{
    private static readonly string[] s_names =
    [
        "APPLICATIONINSIGHTS_STATSBEAT_DISABLED",
        "APPLICATIONINSIGHTS_SDKSTATS_DISABLED",
        "APPLICATIONINSIGHTS_SDKSTATS_DISABLED_ALL",
    ];
    private static readonly Dictionary<string, string?> s_previous = [];

    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        foreach (string name in s_names)
        {
            s_previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, "true");
        }
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        foreach (var entry in s_previous)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }
}

internal sealed class ExporterSettingsScope : IDisposable
{
    private const string Prefix = "Azure.Monitor.OpenTelemetry.Exporter.";
    private readonly Dictionary<string, (bool Switch, object? Data)> _previous = [];
    private readonly Dictionary<FieldInfo, object?> _previousDrainSettings = [];

    internal ExporterSettingsScope(bool ci)
    {
        SetDrainDisabled("DisableEagerDrainForTesting", true);
        SetDrainDisabled("DisableShutdownDrainForTesting", true);
        SetSwitch("DisablePersistOnShutdown", ci);
        SetSwitch("PersistOnForceFlush", !ci);
        string budget = Prefix + "ShutdownDrainBudgetMilliseconds";
        _previous[budget] = (false, AppContext.GetData(budget));
        AppContext.SetData(budget, 0);
    }

    internal void EnableEagerDrain() => SetDrainDisabled("DisableEagerDrainForTesting", false);

    internal void EnableShutdownDrain() => SetDrainDisabled("DisableShutdownDrainForTesting", false);

    private void SetDrainDisabled(string name, bool value)
    {
        Type handler = typeof(AzureMonitorExporterOptions).Assembly
            .GetType("Azure.Monitor.OpenTelemetry.Exporter.Internals.TransmitFromStorageHandler", throwOnError: true)!;
        FieldInfo field = handler.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"Azure exporter test hook '{name}' changed; update the test isolation scope.");
        _previousDrainSettings.TryAdd(field, field.GetValue(null));
        field.SetValue(null, value);
    }

    private void SetSwitch(string suffix, bool value)
    {
        string name = Prefix + suffix;
        AppContext.TryGetSwitch(name, out bool previous);
        _previous[name] = (previous, AppContext.GetData(name));
        AppContext.SetSwitch(name, value);
    }

    public void Dispose()
    {
        foreach (var entry in _previousDrainSettings)
        {
            entry.Key.SetValue(null, entry.Value);
        }
        foreach (var entry in _previous)
        {
            if (!entry.Key.EndsWith("Milliseconds", StringComparison.Ordinal))
            {
                AppContext.SetSwitch(entry.Key, entry.Value.Switch);
            }
            AppContext.SetData(entry.Key, entry.Value.Data);
        }
    }
}

internal sealed class AzureExporterTestScope : IDisposable
{
    internal const string CliEventName = "dotnet/cli/toplevelparser/command";
    internal string InstrumentationKey { get; } = Guid.NewGuid().ToString();
    internal string StorageDirectory { get; } = Path.Combine(Path.GetTempPath(), "SdkAzureExporterTests", Guid.NewGuid().ToString("N"));
    internal ActivitySource Source { get; } = new("SdkAzureExporterTests." + Guid.NewGuid().ToString("N"));
    private readonly List<HttpClient> _clients = [];

    internal AzureExporterTestScope() => Directory.CreateDirectory(StorageDirectory);

    internal void Configure(AzureMonitorExporterOptions options, HttpMessageHandler handler, string? connectionString = null)
    {
        var client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _clients.Add(client);
        options.ConnectionString = connectionString ?? $"InstrumentationKey={InstrumentationKey};IngestionEndpoint=https://telemetry.invalid/";
        options.StorageDirectory = StorageDirectory;
        options.Transport = new HttpClientTransport(client);
        options.EnableLiveMetrics = false;
        options.EnableStandardMetrics = false;
        options.EnablePerformanceCounters = false;
        options.Retry.MaxRetries = 0;
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(5);
    }

    internal TracerProvider CreateProvider(HttpMessageHandler handler, string? connectionString = null) =>
        Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("dotnet-cli-e2e", serviceVersion: "42.0.0-e2e", serviceInstanceId: "test-instance"))
            .AddSource(Source.Name)
            .AddAzureMonitorTraceExporter(options => Configure(options, handler, connectionString))
            .SetSampler(new AlwaysOnSampler())
            .Build();

    internal Activity Emit(string sessionId, ActivityKind kind = ActivityKind.Internal, bool includeEvent = true)
    {
        Activity activity = Source.StartActivity("dotnet build", kind)
            ?? throw new AssertFailedException("The Azure provider did not sample the CLI activity.");
        activity.SetTag("SessionId", sessionId);
        if (includeEvent)
        {
            activity.AddEvent(new ActivityEvent(CliEventName, tags: new ActivityTagsCollection
            {
                ["SessionId"] = sessionId,
                ["verb"] = "build",
                ["event id"] = Guid.NewGuid().ToString(),
            }));
        }
        activity.Stop();
        return activity;
    }

    internal string[] StoredFiles() => Directory.GetFiles(StorageDirectory, "*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".blob", StringComparison.Ordinal) || path.EndsWith(".lock", StringComparison.Ordinal)).ToArray();

    public void Dispose()
    {
        Source.Dispose();
        foreach (HttpClient client in _clients)
        {
            client.Dispose();
        }
        Directory.Delete(StorageDirectory, recursive: true);
    }
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    internal ConcurrentQueue<string> Payloads { get; } = new();
    internal ConcurrentQueue<Uri> RequestUris { get; } = new();
    internal Func<string, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (payload, _) => Task.FromResult(Accept(payload));

    internal JsonElement[] Envelopes => Payloads.SelectMany(Parse).ToArray();

    internal static JsonElement[] Parse(string payload) => payload.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();

    internal static HttpResponseMessage Accept(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            itemsReceived = Parse(payload).Length,
            itemsAccepted = Parse(payload).Length,
            errors = Array.Empty<object>(),
        }), Encoding.UTF8, "application/json"),
    };

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.RequestUri!.Host.Should().Be("telemetry.invalid");
        request.Method.Should().Be(HttpMethod.Post);
        RequestUris.Enqueue(request.RequestUri);
        string payload = await ReadPayloadAsync(request.Content!, cancellationToken);
        Payloads.Enqueue(payload);
        return await Respond(payload, cancellationToken);
    }

    internal static async Task<string> ReadPayloadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        using var input = new MemoryStream(await content.ReadAsByteArrayAsync(cancellationToken));
        using Stream decoded = content.Headers.ContentEncoding.Contains("gzip")
            ? new GZipStream(input, CompressionMode.Decompress) : input;
        using var reader = new StreamReader(decoded, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
