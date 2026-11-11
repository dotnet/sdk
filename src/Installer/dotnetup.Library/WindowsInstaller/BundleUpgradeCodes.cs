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
    // https://github.com/dotnet/aspnetcore/blob/60ef0101bf97cfccb49ede034b38659c15474d1b/src/Installers/Windows/Wix.props#L24
    private static readonly Guid s_aspNetCoreSharedFrameworkNamespace = new("C43D5520-11B3-4D62-B6FE-5D6840B04101");

    /// <summary>Returns the upgrade codes a bundle for <paramref name="component"/> <paramref name="version"/> can have.</summary>
    public static IReadOnlyList<Guid> For(InstallComponent component, ReleaseVersion version, InstallArchitecture architecture)
    {
        string majorMinor = $"{version.Major}.{version.Minor}";
        string rid = $"win-{architecture}";
        return component switch
        {
            // src/Layout/redist/targets/GenerateMSIs.targets (SdkBundleInstallerOutputGuidString).
            InstallComponent.SDK => [GenerateGuid($"Dotnet SDK Bundle Installer {majorMinor}.{version.Patch / 100} {rid}")],
            // https://github.com/dotnet/runtime/blob/4487f7ee4f90a02c8cca7b3614f816e4180167d3/src/installer/pkg/sfx/bundle/Microsoft.NETCore.App.Bundle.bundleproj#L9
            // https://github.com/dotnet/arcade/blob/6e14e95fb045833c796fb858127da08044014e26/src/Microsoft.DotNet.Build.Tasks.Installers/build/wix5/wix.targets#L93-L108
            InstallComponent.Runtime => [GenerateGuid($".NET Core Shared Framework Bundle Installer {majorMinor} {rid}")],
            // https://github.com/dotnet/windowsdesktop/blob/77af2c59998e1d0e14583f1c90bda4f453b1b663/src/windowsdesktop/src/bundle/Wix.targets#L87-L88; older builds used the arcade seed format.
            InstallComponent.WindowsDesktop =>
            [
                GenerateGuid($"Windows Desktop Shared Framework Bundle Installer {architecture} {majorMinor}"),
                GenerateGuid($"Windows Desktop Shared Framework Bundle Installer {majorMinor} {rid}"),
            ],
            // 10.0+: https://github.com/dotnet/aspnetcore/blob/60ef0101bf97cfccb49ede034b38659c15474d1b/src/Framework/App.Runtime/bundle/aspnetcore-runtime-bundle.bundleproj#L11 with the arcade target above.
            // 9.0 and older: https://github.com/dotnet/aspnetcore/blob/4df25a6a4a344556f34bc7312c541be81bc73fe7/src/Installers/Windows/SharedFrameworkBundle/SharedFrameworkBundle.wixproj#L10 with Wix.targets _GenerateBundleGuids.
            InstallComponent.ASPNETCore =>
            [
                GenerateGuid($"ASP.NET Core Shared Framework Bundle Installer {majorMinor} {rid}"),
                // https://github.com/dotnet/aspnetcore/blob/60ef0101bf97cfccb49ede034b38659c15474d1b/eng/tools/RepoTasks/GenerateGuid.cs#L26
                CreateAspNetCoreUuid(s_aspNetCoreSharedFrameworkNamespace, $"{majorMinor},{architecture}".ToLowerInvariant()),
            ],
            _ => [],
        };
    }

    // Copied from https://github.com/dotnet/aspnetcore/blob/60ef0101bf97cfccb49ede034b38659c15474d1b/eng/tools/RepoTasks/Uuid.cs#L16-L78; it hashes with SHA-256 despite saying version 3.
    /// <summary>
    ///   Generates a version 3 UUID given a namespace UUID and name. This is based on the algorithm described in
    ///   RFC 4122 (http://www.apps.ietf.org/rfc/rfc4122.html), section 4.3.
    /// </summary>
    private static Guid CreateAspNetCoreUuid(Guid namespaceId, string name)
    {
        // 1. Convert the name to a canonical sequence of octets (as defined by the standards or conventions of its name space); put the name space ID in network byte order.
        byte[] namespaceBytes = namespaceId.ToByteArray();
        // Octet 0-3
        int timeLow = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(namespaceBytes, 0));
        // Octet 4-5
        short timeMid = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 4));
        // Octet 6-7
        short timeHiVersion = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 6));

        // 2. Compute the hash of the namespace ID concatenated with the name
        byte[] nameBytes = Encoding.Unicode.GetBytes(name);
        byte[] hashBuffer = new byte[namespaceBytes.Length + nameBytes.Length];

        Buffer.BlockCopy(BitConverter.GetBytes(timeLow), 0, hashBuffer, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(timeMid), 0, hashBuffer, 4, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(timeHiVersion), 0, hashBuffer, 6, 2);
        Buffer.BlockCopy(namespaceBytes, 8, hashBuffer, 8, 8);
        Buffer.BlockCopy(nameBytes, 0, hashBuffer, 16, nameBytes.Length);
        byte[] hash;

        using (SHA256 sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(hashBuffer);
        }

        Array.Resize(ref hash, 16);

        // 3. Set octets zero through 3 of the time_low field to octets zero through 3 of the hash.
        timeLow = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(hash, 0));
        Buffer.BlockCopy(BitConverter.GetBytes(timeLow), 0, hash, 0, 4);

        // 4. Set octets zero and one of the time_mid field to octets 4 and 5 of the hash.
        timeMid = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 4));
        Buffer.BlockCopy(BitConverter.GetBytes(timeMid), 0, hash, 4, 2);

        // 5. Set octets zero and one of the time_hi_and_version field to octets 6 and 7 of the hash.
        timeHiVersion = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 6));

        // 6. Set the four most significant bits (bits 12 through 15) of the time_hi_and_version field to the appropriate 4-bit version number from Section 4.1.3.
        timeHiVersion = (short)((timeHiVersion & 0x0fff) | 0x3000);
        Buffer.BlockCopy(BitConverter.GetBytes(timeHiVersion), 0, hash, 6, 2);

        // 7. Set the clock_seq_hi_and_reserved field to octet 8 of the hash.
        // 8. Set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to zero and one, respectively.
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

        // Steps 9-11 are essentially no-ops, but provided for completion sake
        // 9. Set the clock_seq_low field to octet 9 of the hash.
        // 10. Set octets zero through five of the node field to octets 10 through 15 of the hash.
        // 11. Convert the resulting UUID to local byte order.

        return new Guid(hash);
    }
}
