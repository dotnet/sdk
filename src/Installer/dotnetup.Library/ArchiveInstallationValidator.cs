// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ArchiveInstallationValidator : IInstallationValidator
{
    private static readonly Dictionary<InstallComponent, string> s_runtimeMonikerByComponent = new()
    {
        [InstallComponent.Runtime] = InstallComponentExtensions.RuntimeFrameworkName,
        [InstallComponent.ASPNETCore] = InstallComponentExtensions.AspNetCoreFrameworkName,
        [InstallComponent.WindowsDesktop] = InstallComponentExtensions.WindowsDesktopFrameworkName
    };

#pragma warning disable CA1822 // Validate methods implement IInstallationValidator and cannot be made static
    public bool Validate(DotnetInstall install)
    {
        return Validate(install, out _);
    }

    /// <summary>
    /// Validates a .NET installation with detailed failure reasons.
    /// </summary>
    /// <param name="install">The install to validate.</param>
    /// <param name="failureReason">If validation fails, describes what went wrong.</param>
    /// <returns>True if the installation is valid.</returns>
    public bool Validate(DotnetInstall install, out string? failureReason)
    {
        failureReason = null;
        string? installRoot = install.InstallRoot.Path;
        if (string.IsNullOrEmpty(installRoot))
        {
            failureReason = "Install root path is null or empty.";
            return false;
        }

        string dotnetMuxerPath = GetMuxerPath(installRoot);
        if (!File.Exists(dotnetMuxerPath))
        {
            if (install.Component == InstallComponent.WindowsDesktop)
            {
                string resolvedVersionLayout = install.Version.ToString();
                if (ValidateComponentLayout(installRoot, resolvedVersionLayout, install.Component))
                {
                    return true;
                }
            }
            failureReason = $"Muxer not found at '{dotnetMuxerPath}'.";
            return false;
        }

        string resolvedVersion = install.Version.ToString();
        if (!ValidateComponentLayout(installRoot, resolvedVersion, install.Component))
        {
            string expectedDir = GetComponentDirectory(installRoot, install.Component, resolvedVersion) ?? "<unknown>";
            failureReason = $"Component layout validation failed. Expected directory '{expectedDir}' to exist and be non-empty.";
            return false;
        }

        if (!ValidateWithHostFxr(installRoot, install.Version, install.Component, out string? hostFxrFailure))
        {
            failureReason = $"HostFxr validation failed: {hostFxrFailure}";
            return false;
        }

        // We should also validate whether the host is the maximum version or higher than all installed versions.

        return true;
    }
#pragma warning restore CA1822

    private static bool ValidateComponentLayout(string installRoot, string resolvedVersion, InstallComponent component)
    {
        string? componentDirectory = GetComponentDirectory(installRoot, component, resolvedVersion);
        return componentDirectory is not null && DirectoryExistsAndNotEmpty(componentDirectory);
    }

    private static bool DirectoryExistsAndNotEmpty(string path)
    {
        return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
    }

    private static bool ValidateWithHostFxr(string installRoot, ReleaseVersion resolvedVersion, InstallComponent component, out string? failureReason)
    {
        failureReason = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            //  Calling HostFxr is not working on Linux, so don't use it for validation until we fix that
            //  See https://github.com/dotnet/sdk/issues/52821
            return true;
        }

        try
        {
            return IsReportedByHost(HostFxrWrapper.getInfo(installRoot), installRoot, resolvedVersion, component, out failureReason);
        }
        catch (Exception ex)
        {
            failureReason = $"HostFxr threw an exception: {ex}";
            return false;
        }
    }

    /// <summary>Gets the path of the <c>dotnet</c> muxer in an install root.</summary>
    internal static string GetMuxerPath(string installRoot) => Path.Combine(installRoot, DotnetupUtilities.GetDotnetExeName());

    /// <summary>
    /// Gets the directory that holds every version of a component: <c>sdk</c> for SDKs and
    /// <c>shared/&lt;framework name&gt;</c> for runtimes. Returns null for an unknown component.
    /// </summary>
    internal static string? GetComponentBaseDirectory(string installRoot, InstallComponent component)
    {
        if (component == InstallComponent.SDK)
        {
            return Path.Combine(installRoot, "sdk");
        }

        return s_runtimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker)
            ? Path.Combine(installRoot, "shared", runtimeMoniker)
            : null;
    }

    /// <summary>Gets the directory a specific version of a component is installed to, or null for an unknown component.</summary>
    internal static string? GetComponentDirectory(string installRoot, InstallComponent component, string resolvedVersion)
    {
        string? baseDirectory = GetComponentBaseDirectory(installRoot, component);
        return baseDirectory is null ? null : Path.Combine(baseDirectory, resolvedVersion);
    }

    /// <summary>
    /// Checks whether the SDKs and runtimes a host reports for an install root include the given component
    /// version at its expected location in that root. The host's report may come from hostfxr loaded in
    /// process or from the root's muxer run out of process.
    /// </summary>
    internal static bool IsReportedByHost(
        NetEnvironmentInfo environmentInfo,
        string installRoot,
        ReleaseVersion resolvedVersion,
        InstallComponent component,
        out string? failureReason)
    {
        failureReason = null;

        if (component == InstallComponent.SDK)
        {
            string expectedPath = GetComponentDirectory(installRoot, component, resolvedVersion.ToString())!;
            bool found = environmentInfo.SdkInfo.Any(sdk =>
                string.Equals(sdk.Version.ToString(), resolvedVersion.ToString(), StringComparison.OrdinalIgnoreCase) &&
                DotnetupUtilities.PathsEqual(sdk.Path, expectedPath));
            if (!found)
            {
                var availableSdks = string.Join(", ", environmentInfo.SdkInfo.Select(s => $"{s.Version} @ {s.Path}"));
                failureReason = $"HostFxr did not report SDK {resolvedVersion} at '{expectedPath}'. Available SDKs: [{availableSdks}]";
            }
            return found;
        }

        if (!s_runtimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker))
        {
            failureReason = $"No runtime moniker mapping for component '{component}'.";
            return false;
        }

        // The host reports runtime paths like shared/Microsoft.NETCore.App, without the version.
        string expectedRuntimeBasePath = GetComponentBaseDirectory(installRoot, component)!;
        bool runtimeFound = environmentInfo.RuntimeInfo.Any(runtime =>
            string.Equals(runtime.Name, runtimeMoniker, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(runtime.Version.ToString(), resolvedVersion.ToString(), StringComparison.OrdinalIgnoreCase) &&
            DotnetupUtilities.PathsEqual(runtime.Path, expectedRuntimeBasePath));
        if (!runtimeFound)
        {
            var availableRuntimes = string.Join(", ", environmentInfo.RuntimeInfo.Select(r => $"{r.Name} {r.Version} @ {r.Path}"));
            failureReason = $"HostFxr did not report {runtimeMoniker} {resolvedVersion} at '{expectedRuntimeBasePath}'. Available runtimes: [{availableRuntimes}]";
        }
        return runtimeFound;
    }
}