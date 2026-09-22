// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal static class SelfUpdateStartupTelemetryProcess
{
    public const string ChildEnvironmentVariable = "DOTNETUP_STARTUP_TELEMETRY_CHILD";

    public static async Task RunAsync(string testName, string? expectedUtf8Output = null, string? expectedUtf8Error = null)
    {
        using var files = new SelfUpdateTestFiles(executable: false);
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
        using var output = new MemoryStream();
        using var error = new MemoryStream();
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(output);
        var stderr = process.StandardError.BaseStream.CopyToAsync(error);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await Task.WhenAll(process.WaitForExitAsync(timeout.Token), stdout, stderr).WaitAsync(timeout.Token).ConfigureAwait(false);
            Assert.AreEqual(0, process.ExitCode, Encoding.UTF8.GetString(output.ToArray()) + Encoding.UTF8.GetString(error.ToArray()));
            if (expectedUtf8Output is not null)
            {
                Assert.IsGreaterThanOrEqualTo(0, output.ToArray().AsSpan().IndexOf(Encoding.UTF8.GetBytes(expectedUtf8Output)));
            }
            if (expectedUtf8Error is not null)
            {
                Assert.IsGreaterThanOrEqualTo(0, error.ToArray().AsSpan().IndexOf(Encoding.UTF8.GetBytes(expectedUtf8Error)));
            }
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