// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// A detached process for dotnetup's persist-then-drain telemetry to export without causing exit lag.
/// </summary>
internal static class DotnetupTelemetryDrainProcess
{
    /// <summary>
    /// Upper bound on how long a detached drainer runs before giving up.
    /// </summary>
    private static readonly TimeSpan s_drainerLifetime = TimeSpan.FromMinutes(3);

    /// <summary>
    /// When this process was launched as a detached drainer (<see cref="Constants.Telemetry.DrainModeEnvVar"/>
    /// == <c>1</c>), best-effort drains persisted telemetry to Azure Monitor and returns
    /// <see langword="true"/> with exit code <c>0</c>. Drain failures are intentionally swallowed
    /// because telemetry delivery must not affect the host process. Otherwise returns
    /// <see langword="false"/> and does nothing.
    /// </summary>
    public static bool TryRunAsDrainer(out int exitCode)
    {
        exitCode = 0;

        if (!string.Equals(Environment.GetEnvironmentVariable(Constants.Telemetry.DrainModeEnvVar), "1", StringComparison.Ordinal))
        {
            return false;
        }

        if (DotnetupTelemetry.IsTelemetryOptedOut(Environment.GetEnvironmentVariable))
        {
            return true;
        }

        try
        {
            var storageDirectory = DotnetupPaths.ResolveTelemetryStorageDirectory(Environment.GetEnvironmentVariable);
            var connectionString = DotnetupTelemetry.ResolveConnectionString(Environment.GetEnvironmentVariable);

            PersistentStorageTelemetryDrainer
                .RunAsync(connectionString, storageDirectory, s_drainerLifetime)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {

        }
        return true;
    }

    /// <summary>
    /// Spawns a detached copy of the dotnetup executable in drain mode to deliver persisted
    /// telemetry out of band, then returns without waiting. Best-effort: any failure is swallowed
    /// and the persisted blobs are simply delivered by a later run instead.
    /// </summary>
    public static void SpawnDetachedDrainer()
    {
        try
        {
            var executablePath = Environment.ProcessPath;

            // Only relaunch the real dotnetup executable. Under test hosts or `dotnet exec` the process path is testhost/dotnet
            if (string.IsNullOrEmpty(executablePath) || !IsBootstrapperExecutable(executablePath))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                // Give the child fresh stdio pipes instead of inheriting this process's console/redirect handles
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            startInfo.Environment[Constants.Telemetry.DrainModeEnvVar] = "1";

            using var _ = Process.Start(startInfo);
        }
        catch
        {
            // Telemetry delivery must never crash or delay process exit.
        }
    }

    private static bool IsBootstrapperExecutable(string executablePath)
    {
        var name = Path.GetFileNameWithoutExtension(executablePath);
        return string.Equals(name, "dotnetup", StringComparison.OrdinalIgnoreCase);
    }

}
