// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Deployment.DotNet.Releases;
#if NET
using Microsoft.Win32;
#endif

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Determines whether IT policy forbids dnup from downloading from sources that have
/// only a SHA-512 hash and no detached CMS signature (the blob feed used for daily
/// channels and recent prerelease builds).
///
/// Mirrors the workload-signing policy pattern used by the SDK (see
/// <c>src/Cli/dotnet/Commands/Workload/SignCheck_Windows.cs</c>) but applies to
/// dnup downloads and uses a dnup-scoped registry key / config file.
///
/// Test code may override the result via <see cref="OverrideForTesting"/>.
/// </summary>
internal static class UnsignedSourcePolicy
{
    /// <summary>
    /// Registry path checked on Windows. A non-zero <c>REG_DWORD</c> at
    /// <c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup!BlockUnsignedDownloads</c>
    /// enforces the policy.
    /// </summary>
    internal const string WindowsPolicyKey = @"SOFTWARE\Policies\Microsoft\dotnet\Dotnetup";

    /// <summary>The DWORD value name under <see cref="WindowsPolicyKey"/>.</summary>
    internal const string WindowsPolicyValueName = "BlockUnsignedDownloads";

    /// <summary>
    /// Sentinel file checked on Linux/macOS. When this file exists the policy is enforced;
    /// content is ignored.
    /// </summary>
    internal const string UnixPolicyFile = "/etc/dotnet/dnup-block-unsigned-downloads";

    /// <summary>
    /// When non-null, overrides the OS-level check. Intended for tests; setting this from
    /// production code is not supported.
    /// </summary>
    internal static Func<bool>? OverrideForTesting { get; set; }

    // Process-wide state used to surface the "unsigned download" warning exactly once, even when
    // the up-front (channel-based) prediction misses a case that only reveals itself when the
    // downloader actually falls back to the blob feed (e.g. a roll-forward band channel like
    // "11.0.1xx" that resolves to a blob-feed-only preview). 0 = not yet, 1 = done.
    private static int s_unsignedWarningShown;
    private static int s_unsignedFallbackUsed;

    /// <summary>
    /// Atomically claims the right to show the unsigned-download warning. Returns true exactly
    /// once per process; subsequent callers get false so the warning is never shown twice.
    /// </summary>
    public static bool TryClaimUnsignedWarning()
        => Interlocked.CompareExchange(ref s_unsignedWarningShown, 1, 0) == 0;

    /// <summary>
    /// Records that the downloader actually served a request from the unsigned blob feed. Called
    /// from the download path (which cannot write to the console) so a higher layer can show the
    /// warning afterward if it was not already shown up-front.
    /// </summary>
    public static void MarkUnsignedFallbackUsed() => Interlocked.Exchange(ref s_unsignedFallbackUsed, 1);

    /// <summary>
    /// True when at least one request has been served from the unsigned blob feed in this process.
    /// </summary>
    public static bool UnsignedFallbackUsed => Volatile.Read(ref s_unsignedFallbackUsed) != 0;

    /// <summary>
    /// Resets the process-wide warning state. Intended for tests only.
    /// </summary>
    internal static void ResetWarningStateForTesting()
    {
        Interlocked.Exchange(ref s_unsignedWarningShown, 0);
        Interlocked.Exchange(ref s_unsignedFallbackUsed, 0);
    }

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

    /// <summary>
    /// Returns true when the host machine has the "block unsigned downloads" policy enabled
    /// and dnup should refuse to fall back to the blob feed.
    /// </summary>
    public static bool IsUnsignedDownloadBlocked()
    {
        var overrideFn = OverrideForTesting;
        if (overrideFn is not null)
        {
            return overrideFn();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsWindowsPolicyEnabled();
        }

        try
        {
            return File.Exists(UnixPolicyFile);
        }
        catch
        {
            // Reading /etc shouldn't fault, but be defensive — a failed probe must not
            // promote a permissive default into a hard block.
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsWindowsPolicyEnabled()
    {
#if NET
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(WindowsPolicyKey);
            return ((int?)key?.GetValue(WindowsPolicyValueName) ?? 0) != 0;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }
}
