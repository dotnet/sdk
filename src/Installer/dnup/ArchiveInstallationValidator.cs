// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dotnet.Installation.Internal;
using System.Runtime.InteropServices;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
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

        string dotnetMuxerPath = Path.Combine(installRoot, DnupUtilities.GetDotnetExeName());
        if (!File.Exists(dotnetMuxerPath))
        {
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
            return Directory.Exists(sdkDirectory);
        }

        if (RuntimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker))
        {
            string runtimeDirectory = Path.Combine(installRoot, "shared", runtimeMoniker, resolvedVersion);
            return Directory.Exists(runtimeDirectory);
        }

        return false;
    }

    private bool ValidateWithHostFxr(string installRoot, ReleaseVersion resolvedVersion, InstallComponent component)
    {
        try
        {
            var environmentInfo = HostFxrWrapper.getInfo(installRoot);

            if (component == InstallComponent.SDK)
            {
                string expectedPath = Path.Combine(installRoot, "sdk", resolvedVersion.ToString());
                return environmentInfo.SdkInfo.Any(sdk =>
                    string.Equals(sdk.Version.ToString(), resolvedVersion.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    DnupUtilities.PathsEqual(sdk.Path, expectedPath));
            }

            if (!RuntimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker))
            {
                return false;
            }

            string expectedRuntimePath = Path.Combine(installRoot, "shared", runtimeMoniker, resolvedVersion.ToString());
            return environmentInfo.RuntimeInfo.Any(runtime =>
                string.Equals(runtime.Name, runtimeMoniker, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(runtime.Version.ToString(), resolvedVersion.ToString(), StringComparison.OrdinalIgnoreCase) &&
                DnupUtilities.PathsEqual(runtime.Path, expectedRuntimePath));
        }
        catch
        {
            return false;
        }
    }
}
