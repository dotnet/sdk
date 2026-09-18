// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Smoke-tests executable startup with bounded output and execution time; it does not authenticate releases.</summary>
internal static class SelfUpdateVerifier
{
    private static readonly TimeSpan s_terminationTimeout = TimeSpan.FromSeconds(5);

    public static void Verify(string installedPath, TimeSpan timeout)
    {
        try
        {
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            var paths = new SelfUpdatePaths(installedPath);
            paths.Validate();
            VerifyAsync(paths.InstalledPath, timeout).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or
            InvalidOperationException or ArgumentException or NotSupportedException or OperationCanceledException)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.InstallFailed,
                $"Could not verify the updated dotnetup executable '{installedPath}': {exception.Message}", exception);
        }
    }

    private static async Task VerifyAsync(string installedPath, TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(installedPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("--version");
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        if (!process.Start())
        {
            throw new IOException("The verification child could not be started.");
        }

        using var cancellation = new CancellationTokenSource(timeout);
        var stdoutTask = CaptureAsync(process.StandardOutput.BaseStream, 4096, cancellation.Token);
        var stderrTask = CaptureAsync(process.StandardError.BaseStream, 4096, cancellation.Token);
        Exception? failure = null;
        try
        {
            process.StandardInput.Close();
            await Task.WhenAll(process.WaitForExitAsync(cancellation.Token), stdoutTask, stderrTask)
                .WaitAsync(cancellation.Token).ConfigureAwait(false);
            var output = await stdoutTask.ConfigureAwait(false);
            var stdout = Encoding.UTF8.GetString(output).Trim();
            if (process.ExitCode != 0 || output.Length >= 4096 || !Microsoft.Deployment.DotNet.Releases.ReleaseVersion.TryParse(stdout, out _))
            {
                var stderr = Encoding.UTF8.GetString(await stderrTask.ConfigureAwait(false));
                var diagnostic = new string(stderr.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
                throw new IOException($"Verification exited with code {process.ExitCode} or returned invalid version output. Stderr: {diagnostic}");
            }
        }
        catch (OperationCanceledException exception)
        {
            failure = new IOException("The verification child exceeded its timeout.", exception);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await cancellation.CancelAsync().ConfigureAwait(false);
        try
        {
            await TerminateAsync(process).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or OperationCanceledException)
        {
            failure = new IOException("The verification child could not be terminated within five seconds.",
                failure is null ? exception : new AggregateException(failure, exception));
        }

        await DrainAsync(stdoutTask, stderrTask).ConfigureAwait(false);
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static async Task TerminateAsync(Process process)
    {
        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }

            using var termination = new CancellationTokenSource(s_terminationTimeout);
            await process.WaitForExitAsync(termination.Token).ConfigureAwait(false);
        }
    }

    private static async Task DrainAsync(Task<byte[]> stdoutTask, Task<byte[]> stderrTask)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(s_terminationTimeout).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or TimeoutException)
        {
        }
    }

    private static async Task<byte[]> CaptureAsync(Stream stream, int limit, CancellationToken cancellationToken)
    {
        var captured = new byte[limit];
        var buffer = new byte[4096];
        var length = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return captured[..length];
            }

            var keep = Math.Min(read, limit - length);
            buffer.AsSpan(0, keep).CopyTo(captured.AsSpan(length));
            length += keep;
        }
    }
}