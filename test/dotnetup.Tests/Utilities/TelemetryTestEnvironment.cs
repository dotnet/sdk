// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.DotNet.Tools.Bootstrapper;
using OpenTelemetry.PersistentStorage.FileSystem;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class TelemetryTestEnvironment : IDisposable
{
    private readonly TestEnvironment _testEnvironment = new();
    private readonly TaskCompletionSource _outputClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _errorClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TelemetryTestEnvironment(string ingestionEndpoint)
    {
        StorageDirectory = Path.Combine(TempRoot, "telemetry");
        ShutdownBudgetPath = Path.Combine(TempRoot, "shutdown-budget.txt");
        DrainerProcessPath = Path.Combine(TempRoot, "drainer-process.txt");
        EnvironmentVariables = new Dictionary<string, string>
        {
            [Constants.Telemetry.TelemetryOptOutEnvVar] = "0",
            [Constants.Telemetry.StoragePathEnvVar] = StorageDirectory,
            [Constants.Telemetry.ForceLocalDeliveryEnvVar] = "1",
            [Constants.Telemetry.TestDrainerProcessPathEnvVar] = DrainerProcessPath,
            [Constants.Telemetry.DisableTraceExportEnvVar] = "0",
            ["APPLICATIONINSIGHTS_STATSBEAT_DISABLED"] = "true",
            ["APPLICATIONINSIGHTS_SDKSTATS_DISABLED_ALL"] = "true",
            ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = Path.Combine(TempRoot, "data"),
            [Constants.Telemetry.E2EConnectionStringEnvVar] =
                $"InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint={ingestionEndpoint}",
            ["NO_PROXY"] = "127.0.0.1",
        };
    }

    public string TempRoot => _testEnvironment.TempRoot;
    public string StorageDirectory { get; }
    public string ShutdownBudgetPath { get; }
    public string DrainerProcessPath { get; }
    public Dictionary<string, string> EnvironmentVariables { get; }

    public string[] TelemetryBlobPaths =>
        Directory.Exists(StorageDirectory)
            ? Directory.GetFiles(StorageDirectory, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".blob", StringComparison.Ordinal)
                    || path.EndsWith(".lock", StringComparison.Ordinal) && Path.GetFileName(path) != ".drain.lock").ToArray()
            : [];

    public (int exitCode, string output) RunDotnetup(string[] args) =>
        DotnetupTestUtilities.RunDotnetupProcess(
            args,
            captureOutput: true,
            workingDirectory: TempRoot,
            environmentVariables: EnvironmentVariables);

    public (int exitCode, string output) RunEnvScript() =>
        RunDotnetup(
            ["env", "script", "--shell", "pwsh", "--dotnet-install-path", _testEnvironment.InstallPath]);

    public void ConfigureShutdownBudgetObservation() =>
        EnvironmentVariables[Constants.Telemetry.TestShutdownBudgetPathEnvVar] = ShutdownBudgetPath;

    public async Task WaitForDrainerStartedAsync(TimeSpan timeout)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(TempRoot, Path.GetFileName(DrainerProcessPath));
        watcher.Created += (_, _) => completion.TrySetResult();
        watcher.Changed += (_, _) => completion.TrySetResult();
        watcher.EnableRaisingEvents = true;
        if (!File.Exists(DrainerProcessPath))
        {
            await completion.Task.WaitAsync(timeout);
        }
    }

    public async Task WaitForTelemetryBlobsDeletedAsync(TimeSpan timeout)
    {
        if (TelemetryBlobPaths.Length == 0)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(StorageDirectory) { IncludeSubdirectories = true };
        FileSystemEventHandler onChange = (_, _) =>
        {
            if (TelemetryBlobPaths.Length == 0)
            {
                completion.TrySetResult();
            }
        };
        watcher.Deleted += onChange;
        watcher.Renamed += (_, _) => onChange(null!, null!);
        watcher.EnableRaisingEvents = true;

        if (TelemetryBlobPaths.Length == 0)
        {
            return;
        }

        await completion.Task.WaitAsync(timeout);
    }

    public Process StartDrainer()
    {
        var startInfo = new ProcessStartInfo(DotnetupTestUtilities.GetDotnetupExecutablePath())
        {
            UseShellExecute = false,
            WorkingDirectory = TempRoot,
        };
        startInfo.ArgumentList.Add(Constants.Telemetry.DrainCommand);
        foreach (var entry in EnvironmentVariables)
        {
            startInfo.Environment[entry.Key] = entry.Value;
        }
        return Process.Start(startInfo)!;
    }

    public void SeedStoredTelemetry()
    {
        string partition = Directory.GetDirectories(StorageDirectory).Single();
        using var provider = new FileBlobProvider(partition);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            name = "Message",
            time = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            iKey = "00000000-0000-0000-0000-000000000000",
            data = new { baseType = "MessageData", baseData = new { ver = 2, message = "dotnetup/drain-fixture" } },
        });
        Assert.IsTrue(provider.TryCreateBlob(payload.AsSpan(), out _));
    }

    public Process StartCommand(params string[] args)
    {
        var startInfo = new ProcessStartInfo(DotnetupTestUtilities.GetDotnetupExecutablePath())
        {
            UseShellExecute = false,
            WorkingDirectory = TempRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (var entry in EnvironmentVariables)
        {
            startInfo.Environment[entry.Key] = entry.Value;
        }
        var process = Process.Start(startInfo)!;
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                _outputClosed.TrySetResult();
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                _errorClosed.TrySetResult();
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    public Task WaitForCommandOutputClosedAsync(TimeSpan timeout) =>
        Task.WhenAll(_outputClosed.Task, _errorClosed.Task).WaitAsync(timeout);

    public void Dispose()
    {
        if (File.Exists(DrainerProcessPath))
        {
            string[] identity = File.ReadAllText(DrainerProcessPath).Split(':');
            try
            {
                using var child = Process.GetProcessById(int.Parse(identity[0], CultureInfo.InvariantCulture));
                if (child.StartTime.ToUniversalTime().Ticks == long.Parse(identity[1], CultureInfo.InvariantCulture))
                {
                    child.Kill(entireProcessTree: true);
                    child.WaitForExit();
                }
            }
            catch (ArgumentException)
            {
            }
        }
        _testEnvironment.Dispose();
    }
}