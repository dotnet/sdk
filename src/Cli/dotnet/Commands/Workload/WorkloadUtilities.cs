// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadUtilities
{
    internal static int VersionCompare(string first, string second)
    {
        if (first.Equals(second))
        {
            return 0;
        }

        var firstDash = first.IndexOf('-');
        var secondDash = second.IndexOf('-');
        firstDash = firstDash < 0 ? first.Length : firstDash;
        secondDash = secondDash < 0 ? second.Length : secondDash;

        var firstVersion = new Version(first.Substring(0, firstDash));
        var secondVersion = new Version(second.Substring(0, secondDash));

        var comparison = firstVersion.CompareTo(secondVersion);
        if (comparison != 0)
        {
            return comparison;
        }

        var modifiedFirst = new ReleaseVersion(1, 1, 1, firstDash == first.Length ? null : first.Substring(firstDash));
        var modifiedSecond = new ReleaseVersion(1, 1, 1, secondDash == second.Length ? null : second.Substring(secondDash));

        return modifiedFirst.CompareTo(modifiedSecond);
    }

    /// <summary>
    /// Determines whether workload package and installer signatures should be verified,
    /// without considering any user-specified skip flags.
    /// </summary>
    /// <remarks>
    /// Use this overload for non-interactive code paths (background operations, info queries)
    /// where the user has no opportunity to pass <c>--skip-sign-check</c>. This still respects
    /// the registry policy and dotnet host signing status.
    /// </remarks>
    /// <returns><see langword="true"/> if signatures of packages and installers should be verified.</returns>
    public static bool ShouldVerifySignatures() => ShouldVerifySignatures(skipSignCheck: false);

    /// <summary>
    /// Determines whether workload package and installer signatures should be verified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the central policy decision point for signature verification across the workload system.
    /// The result controls <b>both</b> NuGet package signature verification (for <c>.nupkg</c> files downloaded
    /// from feeds) and MSI Authenticode signature verification (for <c>.msi</c> files extracted from those packages
    /// on Windows). See <c>SIGNING-VERIFICATION.md</c> in this directory and
    /// <c>NUGET-SIGNATURE-VERIFICATION.md</c> in the NuGetPackageDownloader directory for the full architecture.
    /// The single boolean is passed as <c>verifySignatures</c> to <c>NuGetPackageDownloader</c> and as
    /// <c>verifyMsiSignature</c> to <c>WorkloadInstallerFactory</c>.
    /// </para>
    /// <para>
    /// Decision tree (Windows only — always returns <see langword="false"/> on other platforms):
    /// <list type="number">
    ///   <item>If the dotnet host binary itself is <b>not</b> Authenticode-signed, return <see langword="false"/>.
    ///         Rationale: if the host is unsigned, the installation is already untrusted, so enforcing downstream
    ///         signatures provides no additional security guarantee.</item>
    ///   <item>If the user specified <c>--skip-sign-check</c> but the <c>VerifySignatures</c> registry policy
    ///         (<c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Workloads</c>) is enabled, throw a
    ///         <see cref="GracefulException"/>. The global policy cannot be overridden by a command-line flag.</item>
    ///   <item>Otherwise, return <c>!skipSignCheck</c> — i.e., verify unless the user explicitly opted out.</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="skipSignCheck">
    /// <see langword="true"/> if the user passed <c>--skip-sign-check</c> on the command line.
    /// </param>
    /// <returns><see langword="true"/> if signatures of packages and installers should be verified.</returns>
    /// <exception cref="GracefulException">
    /// Thrown when the user attempts to skip signature verification while the global policy enforces it.
    /// </exception>
    public static bool ShouldVerifySignatures(bool skipSignCheck)
    {
#if !TARGET_WINDOWS
        // Non-Windows: workload signature verification is not currently enforced for three reasons:
        //   1. SignCheck.IsDotNetSigned() depends on Windows Authenticode APIs (Win32). There is
        //      no equivalent host-signed check on Linux/macOS to anchor the trust chain.
        //   2. NuGet signature verification on Linux uses TRP certificate bundles (.pem files)
        //      shipped as point-in-time snapshots in the SDK. These can lag behind newly-added
        //      roots, creating windows where verification fails for valid packages.
        //   3. On non-Windows, workload packs are file-based (no MSI), so there is no second
        //      verification layer to fall back on.
        //
        // Returning false here means NuGetPackageDownloader will also skip verification (its
        // constructor platform gate only applies when the caller passes verifySignatures: true).
        return false;
#else
        if (!SignCheck.IsDotNetSigned())
        {
            // The dotnet host is unsigned (e.g., a locally-built SDK, preview, or source-build).
            // Skip verification — workload packages are also likely unsigned in this environment,
            // and the user is already running untrusted code.
            return false;
        }

        bool policyEnabled = SignCheck.IsWorkloadSignVerificationPolicySet();
        if (skipSignCheck && policyEnabled)
        {
            // The VerifySignatures registry policy is set by an administrator. It cannot be bypassed
            // by individual users via --skip-sign-check.
            throw new GracefulException(CliCommandStrings.SkipSignCheckInvalidOption);
        }

        // When dotnet is signed: verify by default, unless the user explicitly opted out.
        return !skipSignCheck;
#endif
    }
}
