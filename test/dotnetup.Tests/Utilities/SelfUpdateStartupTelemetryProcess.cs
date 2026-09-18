// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal static class SelfUpdateStartupTelemetryProcess
{
    public const string ChildEnvironmentVariable = "DOTNETUP_STARTUP_TELEMETRY_CHILD";

    public static async Task RunAsync(string testName)
    {
        using var files = new SelfUpdateTestFiles();
        var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = files.Paths.DirectoryPath,
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(startInfo.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(SelfUpdateStartupTelemetryProcess).Assembly.Location);
        }

        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add("FullyQualifiedName=" + testName);
        startInfo.Environment[ChildEnvironmentVariable] = "1";
        startInfo.Environment[Constants.Telemetry.TelemetryOptOutEnvVar] = "0";
        startInfo.Environment[Constants.Telemetry.DisableTraceExportEnvVar] = "1";
        startInfo.Environment[Constants.Telemetry.EnableOtlpExporterEnvVar] = "0";
        startInfo.Environment[Constants.Telemetry.EnablePerfTraceEnvVar] = "0";
        startInfo.Environment[Constants.Telemetry.DiskLogPathEnvVar] = string.Empty;
        startInfo.Environment[Constants.Telemetry.StoragePathEnvVar] = Path.Combine(files.Paths.DirectoryPath, "telemetry");
        startInfo.Environment["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = Path.Combine(files.Paths.DirectoryPath, "data");

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            Assert.AreEqual(0, process.ExitCode, await output.ConfigureAwait(false) + await error.ConfigureAwait(false));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.IsTrue(process.WaitForExit(10_000));
            }
        }
    }
}