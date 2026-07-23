// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class TelemetryTestEnvironment : IDisposable
{
    private readonly TestEnvironment _testEnvironment = new();

    public TelemetryTestEnvironment(string ingestionEndpoint)
    {
        StorageDirectory = Path.Combine(TempRoot, "telemetry");
        ShutdownBudgetPath = Path.Combine(TempRoot, "shutdown-budget.txt");
        EnvironmentVariables = new Dictionary<string, string>
        {
            [Constants.Telemetry.TelemetryOptOutEnvVar] = "0",
            [Constants.Telemetry.StoragePathEnvVar] = StorageDirectory,
            [Constants.Telemetry.ForceLocalDeliveryEnvVar] = "1",
            [Constants.Telemetry.DrainModeEnvVar] = "0",
            ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = Path.Combine(TempRoot, "data"),
            [Constants.Telemetry.E2EConnectionStringEnvVar] =
                $"InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint={ingestionEndpoint}",
            ["NO_PROXY"] = "127.0.0.1",
        };
    }

    public string TempRoot => _testEnvironment.TempRoot;
    public string StorageDirectory { get; }
    public string ShutdownBudgetPath { get; }
    public Dictionary<string, string> EnvironmentVariables { get; }

    public string[] TelemetryBlobPaths =>
        Directory.Exists(StorageDirectory)
            ? Directory.GetFiles(StorageDirectory, "*.blob*")
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

    public async Task WaitForTelemetryBlobsDeletedAsync(TimeSpan timeout)
    {
        if (TelemetryBlobPaths.Length == 0)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(StorageDirectory, "*.blob*");
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

    public void Dispose() => _testEnvironment.Dispose();
}