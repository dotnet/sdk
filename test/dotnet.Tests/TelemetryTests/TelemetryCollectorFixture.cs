// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Microsoft.DotNet.Tests.TelemetryTests;

internal sealed class TelemetryCollectorFixture : IAsyncDisposable
{
    private static readonly string s_aspireCliVersion = typeof(TelemetryCollectorFixture)
        .Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "AspireCliVersion")
        .Value!;
    private static readonly SemaphoreSlim s_toolInstallLock = new(1, 1);

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly StringBuilder _processOutput = new();
    private readonly object _processOutputLock = new();
    private readonly Process _process;

    private TelemetryCollectorFixture(Process process, Uri endpoint, Uri apiEndpoint)
    {
        _process = process;
        Endpoint = endpoint;
        ApiEndpoint = apiEndpoint;
    }

    public Uri Endpoint { get; }

    private Uri ApiEndpoint { get; }

    public static async Task<TelemetryCollectorFixture> CreateAsync(CancellationToken cancellationToken)
    {
        string aspirePath = await GetAspirePathAsync(cancellationToken);
        (int frontendPort, int otlpHttpPort, int otlpGrpcPort) = GetFreeTcpPorts();
        var frontendEndpoint = new Uri($"http://127.0.0.1:{frontendPort}");
        var collector = new TelemetryCollectorFixture(
            StartAspire(aspirePath, frontendEndpoint, otlpHttpPort, otlpGrpcPort),
            new Uri($"http://127.0.0.1:{otlpHttpPort}"),
            new Uri(frontendEndpoint, "/api/telemetry/spans?limit=1000"));

        collector.BeginReadingProcessOutput();

        try
        {
            await collector.WaitUntilReadyAsync(cancellationToken);
            return collector;
        }
        catch
        {
            await collector.DisposeAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<CollectedEvent>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(ApiEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            content,
            cancellationToken: cancellationToken);

        List<CollectedEvent> events = [];
        foreach (JsonElement resourceSpans in document.RootElement
            .GetProperty("data")
            .GetProperty("resourceSpans")
            .EnumerateArray())
        {
            foreach (JsonElement scopeSpans in resourceSpans.GetProperty("scopeSpans").EnumerateArray())
            {
                foreach (JsonElement span in scopeSpans.GetProperty("spans").EnumerateArray())
                {
                    if (!span.TryGetProperty("events", out JsonElement spanEvents))
                    {
                        continue;
                    }

                    foreach (JsonElement spanEvent in spanEvents.EnumerateArray())
                    {
                        events.Add(ParseEvent(spanEvent));
                    }
                }
            }
        }

        // OTLP delivery is at-least-once. Treat retries of the same event ID as one
        // logical telemetry event while preserving independently emitted events.
        return
        [
            .. events
                .GroupBy(
                    e => (e.Name, EventId: e.Attributes.GetValueOrDefault("event id")),
                    e => e)
                .Select(group => group.First())
        ];
    }

    public async Task<IReadOnlyList<CollectedEvent>> WaitForEventsAsync(
        Func<IReadOnlyList<CollectedEvent>, bool> condition,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            ThrowIfAspireExited();

            IReadOnlyList<CollectedEvent> events = await GetEventsAsync(cancellationToken);
            if (condition(events))
            {
                return events;
            }

            await Task.Delay(100, cancellationToken);
        }

        IReadOnlyList<CollectedEvent> collectedEvents = await GetEventsAsync(cancellationToken);
        throw new TimeoutException(
            $"The telemetry condition was not met. Collected events: {string.Join(", ", collectedEvents.Select(e => e.Name))}"
            + Environment.NewLine
            + GetProcessOutput());
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();

        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (_process.HasExited)
            {
            }
        }

        await _process.WaitForExitAsync();
        _process.Dispose();
    }

    private static async Task<string> GetAspirePathAsync(CancellationToken cancellationToken)
    {
        string toolPath = Path.Combine(
            SdkTestContext.Current.TestExecutionDirectory,
            ".tools",
            $"aspire-cli-{s_aspireCliVersion}");

        await s_toolInstallLock.WaitAsync(cancellationToken);
        try
        {
            string? aspirePath = FindAspireExecutable(toolPath);
            if (aspirePath is not null)
            {
                return aspirePath;
            }

            Directory.CreateDirectory(toolPath);
            string dotnetPath = SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;
            string nugetConfigPath = Path.Combine(
                SdkTestContext.Current.TestExecutionDirectory,
                "NuGet.config");

            var startInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("tool");
            startInfo.ArgumentList.Add("install");
            startInfo.ArgumentList.Add("Aspire.Cli");
            startInfo.ArgumentList.Add("--version");
            startInfo.ArgumentList.Add(s_aspireCliVersion);
            startInfo.ArgumentList.Add("--tool-path");
            startInfo.ArgumentList.Add(toolPath);
            startInfo.ArgumentList.Add("--configfile");
            startInfo.ArgumentList.Add(nugetConfigPath);
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

            using Process installProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the Aspire CLI installation.");
            Task<string> standardOutput = installProcess.StandardOutput.ReadToEndAsync();
            Task<string> standardError = installProcess.StandardError.ReadToEndAsync();

            try
            {
                await installProcess.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!installProcess.HasExited)
                {
                    try
                    {
                        installProcess.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (installProcess.HasExited)
                    {
                    }
                }

                await installProcess.WaitForExitAsync();
                await Task.WhenAll(standardOutput, standardError);
                throw;
            }

            string output = await standardOutput;
            string error = await standardError;

            if (installProcess.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Installing Aspire CLI {s_aspireCliVersion} failed with exit code {installProcess.ExitCode}."
                    + Environment.NewLine
                    + output
                    + Environment.NewLine
                    + error);
            }

            return FindAspireExecutable(toolPath)
                ?? throw new FileNotFoundException(
                    $"Aspire CLI {s_aspireCliVersion} was installed without its native executable.",
                    toolPath);
        }
        finally
        {
            s_toolInstallLock.Release();
        }
    }

    private static string? FindAspireExecutable(string toolPath)
    {
        if (!Directory.Exists(toolPath))
        {
            return null;
        }

        string fileName = OperatingSystem.IsWindows() ? "aspire.exe" : "aspire";
        return Directory
            .EnumerateFiles(toolPath, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(path =>
                path.Contains(
                    $"{Path.DirectorySeparatorChar}tools{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}versions{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal));
    }

    private static Process StartAspire(
        string aspirePath,
        Uri frontendEndpoint,
        int otlpHttpPort,
        int otlpGrpcPort)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = aspirePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("dashboard");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--frontend-url");
        startInfo.ArgumentList.Add(frontendEndpoint.ToString());
        startInfo.ArgumentList.Add("--otlp-http-url");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{otlpHttpPort}");
        startInfo.ArgumentList.Add("--otlp-grpc-url");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{otlpGrpcPort}");
        startInfo.ArgumentList.Add("--allow-anonymous");
        startInfo.ArgumentList.Add("--non-interactive");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.Environment["ASPIRE_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Aspire dashboard.");
    }

    private static (int Frontend, int OtlpHttp, int OtlpGrpc) GetFreeTcpPorts()
    {
        TcpListener[] listeners =
        [
            new(IPAddress.Loopback, 0),
            new(IPAddress.Loopback, 0),
            new(IPAddress.Loopback, 0),
        ];

        try
        {
            foreach (TcpListener listener in listeners)
            {
                listener.Start();
            }

            return (
                ((IPEndPoint)listeners[0].LocalEndpoint).Port,
                ((IPEndPoint)listeners[1].LocalEndpoint).Port,
                ((IPEndPoint)listeners[2].LocalEndpoint).Port);
        }
        finally
        {
            foreach (TcpListener listener in listeners)
            {
                listener.Stop();
            }
        }
    }

    private static CollectedEvent ParseEvent(JsonElement spanEvent)
    {
        Dictionary<string, string?> attributes = [];
        if (spanEvent.TryGetProperty("attributes", out JsonElement eventAttributes))
        {
            foreach (JsonElement attribute in eventAttributes.EnumerateArray())
            {
                attributes[attribute.GetProperty("key").GetString()!] =
                    ParseAnyValue(attribute.GetProperty("value"));
            }
        }

        return new CollectedEvent(
            spanEvent.GetProperty("name").GetString()
                ?? throw new InvalidDataException("An OTLP span event did not contain a name."),
            attributes);
    }

    private static string? ParseAnyValue(JsonElement value)
    {
        if (value.TryGetProperty("stringValue", out JsonElement stringValue))
        {
            return stringValue.GetString();
        }

        if (value.TryGetProperty("boolValue", out JsonElement boolValue))
        {
            return boolValue.GetBoolean().ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetProperty("intValue", out JsonElement intValue))
        {
            return intValue.ValueKind == JsonValueKind.String
                ? intValue.GetString()
                : intValue.GetRawText();
        }

        if (value.TryGetProperty("doubleValue", out JsonElement doubleValue))
        {
            return doubleValue.GetDouble().ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    private void BeginReadingProcessOutput()
    {
        _process.OutputDataReceived += AppendProcessOutput;
        _process.ErrorDataReceived += AppendProcessOutput;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private void AppendProcessOutput(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is null)
        {
            return;
        }

        lock (_processOutputLock)
        {
            _processOutput.AppendLine(eventArgs.Data);
        }
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            ThrowIfAspireExited();

            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(ApiEndpoint, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException(
            "The Aspire dashboard API did not become ready."
            + Environment.NewLine
            + GetProcessOutput());
    }

    private void ThrowIfAspireExited()
    {
        if (_process.HasExited)
        {
            throw new InvalidOperationException(
                $"The Aspire dashboard exited unexpectedly with code {_process.ExitCode}."
                + Environment.NewLine
                + GetProcessOutput());
        }
    }

    private string GetProcessOutput()
    {
        lock (_processOutputLock)
        {
            return _processOutput.ToString();
        }
    }
}

internal sealed record CollectedEvent(
    string Name,
    IReadOnlyDictionary<string, string?> Attributes);
