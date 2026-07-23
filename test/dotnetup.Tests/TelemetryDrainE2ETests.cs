// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class TelemetryDrainE2ETests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(20);

    [TestMethod]
    public async Task NativeAotDrainMode_UploadsAndDeletesPersistedTelemetry()
    {
        DotnetupTestUtilities.GetNativeDotnetupExecutablePath();
        string tempRoot = CreateTempDirectory();

        try
        {
            string storageDirectory = Path.Combine(tempRoot, "telemetry");
            using var server = new LoopbackTelemetryServer();
            var environment = CreateEnvironment(tempRoot, storageDirectory, server.IngestionEndpoint);

            CreateRealTelemetryBlob(tempRoot, storageDirectory, environment);
            environment["DOTNETUP_TELEMETRY_DRAIN"] = "1";

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                [], captureOutput: true, workingDirectory: tempRoot, environmentVariables: environment);

            exitCode.Should().Be(0, output);
            await server.WaitForRequestAsync(s_timeout);
            GetTelemetryBlobs(storageDirectory).Should().BeEmpty("accepted telemetry blobs should be deleted");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task NativeAotNormalMode_SpawnsDetachedDrainerThatUploadsTelemetry()
    {
        DotnetupTestUtilities.GetNativeDotnetupExecutablePath();
        string tempRoot = CreateTempDirectory();

        try
        {
            string storageDirectory = Path.Combine(tempRoot, "telemetry");
            string completionPath = Path.Combine(tempRoot, "drain-complete.txt");
            using var server = new LoopbackTelemetryServer();
            var environment = CreateEnvironment(tempRoot, storageDirectory, server.IngestionEndpoint);
            environment["DOTNETUP_TELEMETRY_DRAIN"] = "0";
            environment["DOTNET_TESTHOOK_DOTNETUP_TELEMETRY_DRAIN_COMPLETION_PATH"] = completionPath;
            int parentProcessId = 0;

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                ["--help"], captureOutput: true, workingDirectory: tempRoot, environmentVariables: environment,
                processStarted: processId => parentProcessId = processId);

            exitCode.Should().Be(0, output);
            await WaitForFileAsync(completionPath, s_timeout);
            await server.WaitForRequestAsync(s_timeout);
            File.ReadAllText(completionPath).Should().NotContain($"ProcessId={parentProcessId}{Environment.NewLine}",
                "the completion marker must come from the detached child, not the parent dotnetup process");
            GetTelemetryBlobs(storageDirectory).Should().BeEmpty("the detached child should delete accepted blobs");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static Dictionary<string, string> CreateEnvironment(string tempRoot, string storageDirectory, string ingestionEndpoint) => new()
    {
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "0",
        ["DOTNET_CLI_TELEMETRY_STORAGE_PATH"] = storageDirectory,
        ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = Path.Combine(tempRoot, "data"),
        ["DOTNET_TESTHOOK_DOTNETUP_TELEMETRY_FORCE_LOCAL"] = "1",
        ["DOTNET_TESTHOOK_DOTNETUP_TELEMETRY_CONNECTION_STRING"] =
            $"InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint={ingestionEndpoint}",
        ["NO_PROXY"] = "127.0.0.1",
    };

    private static void CreateRealTelemetryBlob(
        string tempRoot,
        string storageDirectory,
        Dictionary<string, string> environment)
    {
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            ["print-env-script", "--shell", "pwsh", "--dotnet-install-path", Path.Combine(tempRoot, "dotnet")],
            captureOutput: true,
            workingDirectory: tempRoot,
            environmentVariables: environment);

        exitCode.Should().Be(0, output);
        string[] blobs = GetTelemetryBlobs(storageDirectory);
        blobs.Should().NotBeEmpty(
            "the real persistent exporter should write telemetry before drain mode is tested");

        foreach (string extraBlob in blobs.Skip(1))
        {
            File.Delete(extraBlob);
        }
    }

    private static string[] GetTelemetryBlobs(string storageDirectory) =>
        Directory.Exists(storageDirectory)
            ? Directory.GetFiles(storageDirectory).Where(path => !Path.GetFileName(path).StartsWith('.')).ToArray()
            : [];

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "dotnetup-telemetry-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (!File.Exists(path) && Environment.TickCount64 < deadline)
        {
            await Task.Delay(50);
        }

        File.Exists(path).Should().BeTrue($"the detached drainer should write '{path}' within {timeout}");
    }

    private sealed class LoopbackTelemetryServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task _serverTask;

        public LoopbackTelemetryServer()
        {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            IngestionEndpoint = $"http://127.0.0.1:{port}/";
            _serverTask = ServeOneRequestAsync();
        }

        public string IngestionEndpoint { get; }

        public Task WaitForRequestAsync(TimeSpan timeout) => _serverTask.WaitAsync(timeout);

        public void Dispose() => _listener.Stop();

        private async Task ServeOneRequestAsync()
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();
            bool chunked = await ReadHeadersAsync(stream);
            if (chunked)
            {
                await ReadChunkedBodyAsync(stream);
            }

            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response);
            await stream.FlushAsync();
            client.Client.Shutdown(SocketShutdown.Send);
        }

        private static async Task<bool> ReadHeadersAsync(NetworkStream stream)
        {
            bool chunked = false;
            while (true)
            {
                string line = await ReadLineAsync(stream);
                if (line.Length == 0)
                {
                    return chunked;
                }

                chunked |= line.Equals("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static async Task ReadChunkedBodyAsync(NetworkStream stream)
        {
            while (true)
            {
                string sizeLine = await ReadLineAsync(stream);
                int separator = sizeLine.IndexOf(';');
                string sizeText = separator < 0 ? sizeLine : sizeLine[..separator];
                int size = int.Parse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (size == 0)
                {
                    await ReadLineAsync(stream);
                    return;
                }

                await ReadExactlyAsync(stream, size);
                await ReadLineAsync(stream);
            }
        }

        private static async Task<string> ReadLineAsync(NetworkStream stream)
        {
            var bytes = new List<byte>();
            while (true)
            {
                int value = await ReadByteAsync(stream);
                if (value == '\n')
                {
                    return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
                }

                bytes.Add((byte)value);
            }
        }

        private static async Task<int> ReadByteAsync(NetworkStream stream)
        {
            byte[] buffer = new byte[1];
            int read = await stream.ReadAsync(buffer);
            return read == 0 ? throw new EndOfStreamException() : buffer[0];
        }

        private static async Task ReadExactlyAsync(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[Math.Min(length, 8192)];
            while (length > 0)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(length, buffer.Length)));
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                length -= read;
            }
        }
    }
}