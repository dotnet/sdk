// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Security.Cryptography;
using Microsoft.Deployment.DotNet.Releases;
using static Microsoft.DotNet.Build.Tasks.GenerateGuidFromName;

namespace Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

/// <summary>
/// Computes the Burn <c>UpgradeCode</c> that .NET installer bundles share within an upgrade family. A bundle only
/// refuses to downgrade bundles with the same upgrade code, and the product builds derive the code from a fixed name
/// that must not change within a family, or in-place upgrades would break.
/// </summary>
internal static class BundleUpgradeCodes
{
    // aspnetcore src/Installers/Windows/Wix.props: SharedFrameworkNamespaceGuid ("DO NOT CHANGE").
    private static readonly Guid s_aspNetCoreSharedFrameworkNamespace = new("C43D5520-11B3-4D62-B6FE-5D6840B04101");

    /// <summary>Returns the upgrade codes a bundle for <paramref name="component"/> <paramref name="version"/> can have.</summary>
    public static IReadOnlyList<Guid> For(InstallComponent component, ReleaseVersion version, InstallArchitecture architecture)
    {
        string majorMinor = $"{version.Major}.{version.Minor}";
        string rid = $"win-{architecture}";
        return component switch
        {
            // sdk src/Layout/redist/targets/GenerateMSIs.targets: SdkBundleInstallerOutputGuidString.
            InstallComponent.SDK => [GenerateGuid($"Dotnet SDK Bundle Installer {majorMinor}.{version.Patch / 100} {rid}")],
            // runtime src/installer/pkg/sfx/bundle/Microsoft.NETCore.App.Bundle.bundleproj seed, with arcade wix5/wix.targets GetBundleUpgradeCode.
            InstallComponent.Runtime => [GenerateGuid($".NET Core Shared Framework Bundle Installer {majorMinor} {rid}")],
            // windowsdesktop src/windowsdesktop/src/bundle/Wix.targets; older builds used the arcade seed format.
            InstallComponent.WindowsDesktop =>
            [
                GenerateGuid($"Windows Desktop Shared Framework Bundle Installer {architecture} {majorMinor}"),
                GenerateGuid($"Windows Desktop Shared Framework Bundle Installer {majorMinor} {rid}"),
            ],
            // aspnetcore src/Installers/Windows/Wix.targets: _GenerateBundleGuids.
            InstallComponent.ASPNETCore => [GenerateAspNetCoreGuid($"{majorMinor},{architecture}")],
            _ => [],
        };
    }

    // Mirrors aspnetcore's eng/tools/RepoTasks/Uuid.cs, which hashes with SHA-256 despite setting version 3.
    internal static Guid GenerateAspNetCoreGuid(string name)
    {
        byte[] namespaceBytes = s_aspNetCoreSharedFrameworkNamespace.ToByteArray();
        byte[] buffer = new byte[16];
        BitConverter.GetBytes(IPAddress.HostToNetworkOrder(BitConverter.ToInt32(namespaceBytes, 0))).CopyTo(buffer, 0);
        BitConverter.GetBytes(IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 4))).CopyTo(buffer, 4);
        BitConverter.GetBytes(IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 6))).CopyTo(buffer, 6);
        Array.Copy(namespaceBytes, 8, buffer, 8, 8);

        byte[] hash = SHA256.HashData([.. buffer, .. Encoding.Unicode.GetBytes(name.ToLowerInvariant())])[..16];
        BitConverter.GetBytes(IPAddress.NetworkToHostOrder(BitConverter.ToInt32(hash, 0))).CopyTo(hash, 0);
        BitConverter.GetBytes(IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 4))).CopyTo(hash, 4);
        short timeHiVersion = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 6));
        BitConverter.GetBytes((short)((timeHiVersion & 0x0FFF) | 0x3000)).CopyTo(hash, 6);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash);
    }
}
