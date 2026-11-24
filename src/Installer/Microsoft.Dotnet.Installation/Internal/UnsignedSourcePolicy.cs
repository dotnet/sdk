// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Describes downloads from sources that have only a SHA-512 hash and no detached CMS
/// signature (the blob feed used for daily channels and recent prerelease builds), and
/// where IT policy can forbid them. Whether the policy is enforced is read through
/// <see cref="DotnetupPolicy.IsUnsignedDownloadBlocked"/>.
///
/// Mirrors the workload-signing policy pattern used by the SDK (see
/// <c>src/Cli/dotnet/Commands/Workload/SignCheck_Windows.cs</c>) but applies to
/// dnup downloads and uses a dnup-scoped registry key / config file.
/// </summary>
internal static class UnsignedSourcePolicy
{
    /// <summary>
    /// Registry path checked on Windows. A non-zero <c>REG_DWORD</c> at
    /// <c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup!BlockUnsignedDownloads</c>
    /// enforces the policy.
    /// </summary>
    internal const string WindowsPolicyKey = DotnetupPolicy.WindowsPolicyKey;

    /// <summary>The DWORD value name under <see cref="WindowsPolicyKey"/>.</summary>
    internal const string WindowsPolicyValueName = DotnetupPolicy.BlockUnsignedDownloadsValueName;

    /// <summary>
    /// Sentinel file checked on Linux/macOS. When this file exists the policy is enforced;
    /// content is ignored.
    /// </summary>
    internal const string UnixPolicyFile = "/etc/dotnet/dnup-block-unsigned-downloads";

    /// <summary>
    /// Returns the location an administrator removes to clear the policy on the current OS:
    /// the full registry value path on Windows, or <see cref="UnixPolicyFile"/> elsewhere.
    /// </summary>
    internal static string GetPolicyLocationForDisplay() =>
        OperatingSystem.IsWindows()
            ? $@"HKLM\{WindowsPolicyKey}\{WindowsPolicyValueName}"
            : UnixPolicyFile;

    /// <summary>
    /// Returns true when the given install request will (or may) be served from a source
    /// that has no detached CMS signature — the public blob feed used for daily channels
    /// and unknown prerelease versions. Predicate is purely channel-based so it can be
    /// evaluated up-front, before any HTTP or manifest probe, to decide whether to warn
    /// or block before starting the install.
    /// </summary>
    public static bool MayDownloadUnsigned(DotnetInstallRequest request)
    {
        var channel = request.Channel;
        if (channel.IsDaily)
        {
            return true;
        }

        // Fully-specified prerelease versions (e.g. "10.0.100-preview.4.25216.37") fall back
        // to the blob feed when the release manifest doesn't list them. Stable versions and
        // named channels are served only from the signed manifest.
        return channel.IsFullySpecifiedVersion()
            && ReleaseVersion.TryParse(channel.Name, out var parsed)
            && !string.IsNullOrEmpty(parsed.Prerelease);
    }
}
