// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

/// <summary>
/// Lists the SDKs and runtimes in an install root by running that root's <c>dotnet</c> muxer with
/// <c>--list-sdks</c> and <c>--list-runtimes</c> in a child process. Querying out of process keeps
/// dotnetup from loading the root's hostfxr, which would lock files the Windows installer must replace.
/// </summary>
internal static class MuxerInstallQuery
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(60);

    public static IEnumerable<DotnetInstall> GetInstalls(string installRoot)
    {
        string muxerPath = Path.Combine(installRoot, DotnetupUtilities.GetDotnetExeName());
        if (!File.Exists(muxerPath))
        {
            return [];
        }

        var root = new DotnetInstallRoot(installRoot, InstallerUtilities.GetDefaultInstallArchitecture());
        var installs = new List<DotnetInstall>();
        installs.AddRange(ParseSdks(Run(muxerPath, "--list-sdks"), root));
        installs.AddRange(ParseRuntimes(Run(muxerPath, "--list-runtimes"), root));
        return installs;
    }

    /// <summary>Parses lines such as <c>10.0.100 [C:\Program Files\dotnet\sdk]</c>.</summary>
    internal static IEnumerable<DotnetInstall> ParseSdks(string output, DotnetInstallRoot root)
    {
        foreach (string[] parts in SplitLines(output, expectedTokens: 1))
        {
            if (ReleaseVersion.TryParse(parts[0], out var version))
            {
                yield return new DotnetInstall(root, version, InstallComponent.SDK);
            }
        }
    }

    /// <summary>Parses lines such as <c>Microsoft.NETCore.App 10.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]</c>.</summary>
    internal static IEnumerable<DotnetInstall> ParseRuntimes(string output, DotnetInstallRoot root)
    {
        foreach (string[] parts in SplitLines(output, expectedTokens: 2))
        {
            if (InstallComponentExtensions.FromFrameworkName(parts[0]) is InstallComponent component &&
                ReleaseVersion.TryParse(parts[1], out var version))
            {
                yield return new DotnetInstall(root, version, component);
            }
        }
    }

    private static IEnumerable<string[]> SplitLines(string output, int expectedTokens)
    {
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > expectedTokens)
            {
                yield return parts;
            }
        }
    }

    private static string Run(string muxerPath, string argument)
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
        if (!process.WaitForExit(s_timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'{muxerPath} {argument}' did not exit within {s_timeout.TotalSeconds} seconds.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{muxerPath} {argument}' exited with code {process.ExitCode}.");
        }

        return stdout.GetAwaiter().GetResult();
    }
}
