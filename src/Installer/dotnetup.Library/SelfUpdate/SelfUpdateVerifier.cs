// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Reads the full version via executable startup with bounded execution time; it does not authenticate releases.</summary>
internal static class SelfUpdateVerifier
{
    internal const string Utf8EnvironmentVariable = "DOTNETUP_PRIVATE_VERSION_UTF8";

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
        using var standardInput = File.OpenNullHandle();
        var startInfo = CreateStartInfo(installedPath);
        startInfo.StandardInputHandle = standardInput;
        using var cancellation = new CancellationTokenSource(timeout);
        using var process = Process.Start(startInfo)!;
        try
        {
            var (stdout, stderr) = await process.ReadAllTextAsync(cancellation.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);

            var version = stdout.Trim();
            if (process.ExitCode != 0 || !ReleaseVersion.TryParse(version, out _))
            {
                var diagnostic = new string(stderr.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
                throw new IOException($"Verification exited with code {process.ExitCode} or returned invalid version output. Stderr: {diagnostic}");
            }

            return version;
        }
        catch (OperationCanceledException exception)
        {
            throw new IOException("The verification child exceeded its timeout.", exception);
        }
        finally
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

                using var termination = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(termination.Token).ConfigureAwait(false);
            }
        }
    }

    private static ProcessStartInfo CreateStartInfo(string installedPath)
    {
        var startInfo = new ProcessStartInfo(installedPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("--version");
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment[Utf8EnvironmentVariable] = "1";
        return startInfo;
    }
}