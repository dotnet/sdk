// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Validates that the dotnet host on PATH can understand global.json sdk.paths.
/// The sdk.paths feature is consumed by the host, so installing a local SDK is not enough when the host is older than .NET 10.
/// </summary>
internal static class LocalSdkHostValidator
{
    public static void EnsureHostSupportsSdkPaths()
    {
        string version = GetDotnetHostVersion();
        if (!TryGetMajorVersion(version, out int major) || major < 10)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $".NET SDK resolution through global.json sdk.paths requires a .NET 10 or later host on PATH. The current 'dotnet --info' Host.Version is '{version}'.");
        }
    }

    internal static bool TryGetMajorVersion(string version, out int major)
    {
        major = 0;
        int dotIndex = version.IndexOf('.', StringComparison.Ordinal);
        string majorPart = dotIndex >= 0 ? version.Substring(0, dotIndex) : version;
        return int.TryParse(majorPart, NumberStyles.None, CultureInfo.InvariantCulture, out major);
    }

    private static string GetDotnetHostVersion()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "dotnetup-host-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            return GetDotnetHostVersion(workingDirectory);
        }
        finally
        {
            Directory.Delete(workingDirectory);
        }
    }

    private static string GetDotnetHostVersion(string workingDirectory)
    {
        try
        {
            string output = RunDotnetInfo(workingDirectory);
            if (TryGetHostVersionFromInfo(output, out string hostVersion))
            {
                return hostVersion;
            }

            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                "Unable to verify that the dotnet host on PATH supports global.json sdk.paths because 'dotnet --info' did not report a Host.Version value.");
        }
        catch (Win32Exception ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                "Unable to verify global.json sdk.paths support because the dotnet host was not found on PATH. Install .NET 10 or later first.",
                ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                "Unable to verify global.json sdk.paths support because dotnet could not be started.",
                ex);
        }
    }

    private static string RunDotnetInfo(string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = "--info";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = workingDirectory;

        if (!process.Start())
        {
            throw CreateDotnetNotFoundException();
        }

        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            string details = string.IsNullOrWhiteSpace(error) ? "no version output was produced" : error;
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"Unable to verify that the dotnet host on PATH supports global.json sdk.paths because 'dotnet --info' failed: {details}");
        }

        return output;
    }

    private static DotnetInstallException CreateDotnetNotFoundException()
        => new(
            DotnetInstallErrorCode.ContextResolutionFailed,
            "Unable to verify global.json sdk.paths support because the dotnet host was not found on PATH. Install .NET 10 or later first.");

    internal static bool TryGetHostVersionFromInfo(string dotnetInfo, out string hostVersion)
    {
        hostVersion = string.Empty;
        bool inHostSection = false;

        foreach (string line in dotnetInfo.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            string trimmed = line.Trim();
            if (string.Equals(trimmed, "Host:", StringComparison.Ordinal))
            {
                inHostSection = true;
                continue;
            }

            if (!inHostSection)
            {
                continue;
            }

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!trimmed.StartsWith("Version:", StringComparison.Ordinal))
            {
                if (!char.IsWhiteSpace(line[0]))
                {
                    inHostSection = false;
                }

                continue;
            }

            hostVersion = trimmed["Version:".Length..].Trim();
            return !string.IsNullOrWhiteSpace(hostVersion);
        }

        return false;
    }
}
