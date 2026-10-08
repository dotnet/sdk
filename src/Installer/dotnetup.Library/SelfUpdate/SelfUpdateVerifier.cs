// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Reads the full version via executable startup with bounded output and execution time; it does not authenticate releases.</summary>
internal static class SelfUpdateVerifier
{
    internal const string Utf8EnvironmentVariable = "DOTNETUP_PRIVATE_VERSION_UTF8";
    private static readonly TimeSpan s_terminationTimeout = TimeSpan.FromSeconds(5);

    public static string ReadVersion(string installedPath) => ReadVersion(installedPath, TimeSpan.FromSeconds(15));

    /// <summary>
    /// Reads the installed version for identity comparisons without proving that the executable starts.
    /// Windows reads the PE version resource in-process, whose product version is the same informational
    /// version that <c>--version</c> prints; other platforms, and Windows files without a parseable
    /// resource, run <c>--version</c>. Use <see cref="ReadVersion(string)"/> to verify startup.
    /// </summary>
    public static string ReadInstalledVersion(string installedPath)
        => OperatingSystem.IsWindows() && TryReadVersionResource(installedPath, out var version)
            ? version
            : ReadVersion(installedPath);

    private static bool TryReadVersionResource(string installedPath, [NotNullWhen(true)] out string? version)
    {
        version = null;
        try
        {
            installedPath = SelfUpdatePaths.ResolvePath(installedPath);
            using (SelfUpdatePaths.OpenFile(installedPath))
            {
            }

            var productVersion = FileVersionInfo.GetVersionInfo(installedPath).ProductVersion;
            if (productVersion is null || !ReleaseVersion.TryParse(productVersion, out _))
            {
                return false;
            }

            version = productVersion;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // ReadVersion repeats the file checks and reports the failure consistently.
            return false;
        }
    }

    public static string ReadVersion(string installedPath, TimeSpan timeout)
    {
        try
        {
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            installedPath = SelfUpdatePaths.ResolvePath(installedPath);
            using (SelfUpdatePaths.OpenFile(installedPath))
            {
            }

            return ReadVersionAsync(installedPath, timeout).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or
            InvalidOperationException or ArgumentException or NotSupportedException or OperationCanceledException)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.InstallFailed,
                $"Could not read the dotnetup version from executable '{installedPath}': {exception.Message}", exception);
        }
    }

    private static async Task<string> ReadVersionAsync(string installedPath, TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(installedPath),
        };
        if (!process.Start())
        {
            throw new IOException("The verification child could not be started.");
        }

        using var cancellation = new CancellationTokenSource(timeout);
        var stdoutTask = CaptureAsync(process.StandardOutput.BaseStream, 4096, cancellation.Token);
        var stderrTask = CaptureAsync(process.StandardError.BaseStream, 4096, cancellation.Token);
        Exception? failure = null;
        string? version = null;
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

            version = stdout;
        }
        catch (OperationCanceledException exception)
        {
            failure = new IOException("The verification child exceeded its timeout.", exception);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await CompleteProcessAsync(process, cancellation, stdoutTask, stderrTask, failure).ConfigureAwait(false);
        return version!;
    }

    private static async Task CompleteProcessAsync(
        Process process,
        CancellationTokenSource cancellation,
        Task<byte[]> stdoutTask,
        Task<byte[]> stderrTask,
        Exception? failure)
    {
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

    private static ProcessStartInfo CreateStartInfo(string installedPath)
    {
        var startInfo = new ProcessStartInfo(installedPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--version");
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment[Utf8EnvironmentVariable] = "1";
        return startInfo;
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