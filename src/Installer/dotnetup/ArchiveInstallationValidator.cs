// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ArchiveInstallationValidator : IInstallationValidator
{
    private const string HostFxrRuntimeProperty = "HOSTFXR_PATH";
    private static readonly Dictionary<InstallComponent, string> RuntimeMonikerByComponent = new()
    {
        [InstallComponent.Runtime] = "Microsoft.NETCore.App",
        [InstallComponent.ASPNETCore] = "Microsoft.AspNetCore.App",
        [InstallComponent.WindowsDesktop] = "Microsoft.WindowsDesktop.App"
    };

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

        string dotnetMuxerPath = Path.Combine(installRoot, DotnetupUtilities.GetDotnetExeName());
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
            string expectedDir = install.Component == InstallComponent.SDK
                ? Path.Combine(installRoot, "sdk", resolvedVersion)
                : RuntimeMonikerByComponent.TryGetValue(install.Component, out string? moniker)
                    ? Path.Combine(installRoot, "shared", moniker, resolvedVersion)
                    : "<unknown>";
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

    private static bool ValidateComponentLayout(string installRoot, string resolvedVersion, InstallComponent component)
    {
        if (component == InstallComponent.SDK)
        {
            string sdkDirectory = Path.Combine(installRoot, "sdk", resolvedVersion);
            return DirectoryExistsAndNotEmpty(sdkDirectory);
        }

        if (RuntimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker))
        {
            string runtimeDirectory = Path.Combine(installRoot, "shared", runtimeMoniker, resolvedVersion);
            return DirectoryExistsAndNotEmpty(runtimeDirectory);
        }

        return false;
    }

    private static bool DirectoryExistsAndNotEmpty(string path)
    {
        return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
    }

    /// <summary>
    /// Checks if the component files already exist on disk (e.g., from an SDK install that includes the runtime).
    /// This is a lightweight check that doesn't validate the full installation integrity.
    /// </summary>
    public static bool ComponentFilesExist(DotnetInstall install)
    {
        string? installRoot = install.InstallRoot.Path;
        if (string.IsNullOrEmpty(installRoot))
        {
            return false;
        }

        return ValidateComponentLayout(installRoot, install.Version.ToString(), install.Component);
    }

    private bool ValidateWithHostFxr(string installRoot, ReleaseVersion resolvedVersion, InstallComponent component, out string? failureReason)
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
            var environmentInfo = HostFxrWrapper.getInfo(installRoot);

            if (component == InstallComponent.SDK)
            {
                string expectedPath = Path.Combine(installRoot, "sdk", resolvedVersion.ToString());
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

            if (!RuntimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker))
            {
                failureReason = $"No runtime moniker mapping for component '{component}'.";
                return false;
            }

            // The HostFxr returns paths like shared/Microsoft.NETCore.App (without version)
            // but when comparing, we need to account for this
            string expectedRuntimeBasePath = Path.Combine(installRoot, "shared", runtimeMoniker);
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
        catch (Exception ex)
        {
            failureReason = $"HostFxr threw an exception: {ex}";
            return false;
        }
    }
}
