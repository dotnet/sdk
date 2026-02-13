// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.NativeWrapper;

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
        string? installRoot = install.InstallRoot.Path;
        if (string.IsNullOrEmpty(installRoot))
        {
            return false;
        }

        string dotnetMuxerPath = Path.Combine(installRoot, DotnetupUtilities.GetDotnetExeName());
        if (!File.Exists(dotnetMuxerPath))
        {
            // Windows Desktop archive doesn't include the muxer or core runtime.
            // If the component layout is correct, we can still consider the install valid.
            if (install.Component == InstallComponent.WindowsDesktop)
            {
                string resolvedVersionLayout = install.Version.ToString();
                if (ValidateComponentLayout(installRoot, resolvedVersionLayout, install.Component))
                {
                    return true;
                }
            }
            return false;
        }

        string resolvedVersion = install.Version.ToString();
        if (!ValidateComponentLayout(installRoot, resolvedVersion, install.Component))
        {
            return false;
        }

        if (!ValidateWithHostFxr(installRoot, install.Version, install.Component))
        {
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

    private bool ValidateWithHostFxr(string installRoot, ReleaseVersion resolvedVersion, InstallComponent component)
    {
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
                return environmentInfo.SdkInfo.Any(sdk =>
                    string.Equals(sdk.Version.ToString(), resolvedVersion.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    DotnetupUtilities.PathsEqual(sdk.Path, expectedPath));
            }

            if (!RuntimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker))
            {
                return false;
            }

            // The HostFxr returns paths like shared/Microsoft.NETCore.App (without version)
            // but when comparing, we need to account for this
            string expectedRuntimeBasePath = Path.Combine(installRoot, "shared", runtimeMoniker);
            return environmentInfo.RuntimeInfo.Any(runtime =>
                string.Equals(runtime.Name, runtimeMoniker, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(runtime.Version.ToString(), resolvedVersion.ToString(), StringComparison.OrdinalIgnoreCase) &&
                DotnetupUtilities.PathsEqual(runtime.Path, expectedRuntimeBasePath));
        }
        catch
        {
            return false;
        }
    }
}
