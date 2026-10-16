// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Validates an installation by asking the install root's hostfxr, through that root's muxer in a child
/// process, which SDKs and runtimes it can see. Used for installations laid down by the Windows installer
/// bundles, whose layout dotnetup does not own. Querying out of process keeps dotnetup from loading the
/// root's hostfxr, which would lock files the Windows installer must replace.
/// The check of what the host reports is shared with <see cref="ArchiveInstallationValidator"/>.
/// Never throws: a missing install root, a missing hostfxr, or a query failure reports "not installed".
/// </summary>
internal sealed class HostFxrInstallationValidator : IInstallationValidator
{
    private static readonly TimeSpan s_muxerTimeout = TimeSpan.FromSeconds(60);

    private readonly Func<string, NetEnvironmentInfo> _getEnvironmentInfo;

    public HostFxrInstallationValidator()
        : this(GetEnvironmentInfoFromMuxer)
    {
    }

    internal HostFxrInstallationValidator(Func<string, NetEnvironmentInfo> getEnvironmentInfo)
    {
        _getEnvironmentInfo = getEnvironmentInfo;
    }

    public bool Validate(DotnetInstall install) => Validate(install, out _);

    public bool Validate(DotnetInstall install, out string? failureReason)
    {
        string? installRoot = install.InstallRoot.Path;
        if (string.IsNullOrEmpty(installRoot) || !Directory.Exists(installRoot))
        {
            failureReason = $"Install root '{installRoot}' does not exist.";
            return false;
        }

        if (!HasHostFxr(installRoot))
        {
            failureReason = $"No hostfxr library was found under '{Path.Combine(installRoot, "host", "fxr")}'.";
            return false;
        }

        NetEnvironmentInfo environmentInfo;
        try
        {
            environmentInfo = _getEnvironmentInfo(installRoot);
        }
        catch (Exception ex)
        {
            failureReason = $"hostfxr could not enumerate the installations at '{installRoot}': {ex.Message}";
            return false;
        }

        return ArchiveInstallationValidator.IsReportedByHost(environmentInfo, installRoot, install.Version, install.Component, out failureReason);
    }

    /// <summary>
    /// Checks the layout hostfxr is loaded from, <c>host/fxr/&lt;version&gt;/&lt;hostfxr library&gt;</c>, without
    /// searching deeper than the version directories.
    /// </summary>
    private static bool HasHostFxr(string installRoot)
    {
        string hostFxrDirectory = Path.Combine(installRoot, "host", "fxr");
        if (!Directory.Exists(hostFxrDirectory))
        {
            return false;
        }

        string libraryName = OperatingSystem.IsWindows()
            ? "hostfxr.dll"
            : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";

        // hostfxr lives directly in host/fxr/<version>; only look there.
        try
        {
            return Directory.EnumerateDirectories(hostFxrDirectory)
                .Any(versionDirectory => File.Exists(Path.Combine(versionDirectory, libraryName)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Lists the SDKs and runtimes in an install root by running that root's muxer with <c>--list-sdks</c>
    /// and <c>--list-runtimes</c> in a child process. Returns an empty result when the root has no muxer.
    /// </summary>
    private static NetEnvironmentInfo GetEnvironmentInfoFromMuxer(string installRoot)
    {
        string muxerPath = ArchiveInstallationValidator.GetMuxerPath(installRoot);
        if (!File.Exists(muxerPath))
        {
            return new NetEnvironmentInfo();
        }

        return ParseMuxerListOutput(RunMuxer(muxerPath, "--list-sdks"), RunMuxer(muxerPath, "--list-runtimes"));
    }

    /// <summary>
    /// Parses <c>--list-sdks</c> lines such as <c>10.0.100 [C:\Program Files\dotnet\sdk]</c> and
    /// <c>--list-runtimes</c> lines such as <c>Microsoft.NETCore.App 10.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]</c>
    /// into the same shape hostfxr reports in process: SDK paths include the version directory and runtime
    /// paths do not. Malformed lines are skipped.
    /// </summary>
    internal static NetEnvironmentInfo ParseMuxerListOutput(string sdkOutput, string runtimeOutput)
    {
        var sdks = new List<NetSdkInfo>();
        foreach (var (tokens, path) in ParseListLines(sdkOutput))
        {
            if (tokens.Length == 1 && ReleaseVersion.TryParse(tokens[0], out _))
            {
                sdks.Add(new NetSdkInfo(tokens[0], Path.Combine(path, tokens[0])));
            }
        }

        var runtimes = new List<NetRuntimeInfo>();
        foreach (var (tokens, path) in ParseListLines(runtimeOutput))
        {
            if (tokens.Length == 2 && ReleaseVersion.TryParse(tokens[1], out _))
            {
                runtimes.Add(new NetRuntimeInfo(tokens[0], tokens[1], path));
            }
        }

        return new NetEnvironmentInfo(runtimes, sdks);
    }

    /// <summary>Splits each <c>tokens [path]</c> line into its space-separated tokens and its bracketed path.</summary>
    private static IEnumerable<(string[] Tokens, string Path)> ParseListLines(string output)
    {
        foreach (string rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            int pathStart = line.IndexOf(" [", StringComparison.Ordinal);
            if (pathStart <= 0 || !line.EndsWith(']'))
            {
                continue;
            }

            string[] tokens = line[..pathStart].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            yield return (tokens, line[(pathStart + 2)..^1]);
        }
    }

    private static string RunMuxer(string muxerPath, string argument)
    {
        var startInfo = new ProcessStartInfo(muxerPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(muxerPath)!,
        };
        startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{muxerPath}'.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(s_muxerTimeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'{muxerPath} {argument}' did not exit within {s_muxerTimeout.TotalSeconds} seconds.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{muxerPath} {argument}' exited with code {process.ExitCode}.");
        }

        return stdout.GetAwaiter().GetResult();
    }
}
