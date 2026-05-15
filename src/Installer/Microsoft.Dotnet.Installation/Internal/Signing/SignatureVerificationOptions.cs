// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// Configuration for CMS detached-signature verification of release manifest JSON files.
/// Callers must supply trusted root certificate collections; this library does not bundle
/// them itself (dnup wires in the SDK's <c>codesignctl.pem</c>/<c>timestampctl.pem</c> via
/// <see cref="DefaultSignatureOptions"/> + <see cref="TrustedRootsLoader"/>).
/// </summary>
internal sealed class SignatureVerificationOptions
{
    /// <summary>Root certificates trusted as anchors for the code-signing chain (required).</summary>
    public X509Certificate2Collection TrustedCodeSigningRoots { get; }

    /// <summary>Root certificates trusted as anchors for the RFC 3161 timestamp chain (required).</summary>
    public X509Certificate2Collection TrustedTimestampRoots { get; }

    /// <summary>
    /// Controls certificate revocation checking behavior.
    /// Default: <see cref="RevocationCheckMode.Online"/> — fail closed when CRL/OCSP is unreachable.
    /// </summary>
    public RevocationCheckMode RevocationMode { get; init; } = RevocationCheckMode.Online;

    /// <summary>
    /// When <see cref="RevocationMode"/> is not <see cref="RevocationCheckMode.Online"/>,
    /// reject any TSA timestamp older than <c>UtcNow - MaxAcceptableSigningAge</c>.
    /// Provides a time-bounded trust window when revocation cannot be checked.
    /// Ignored when <see cref="RevocationMode"/> is <see cref="RevocationCheckMode.Online"/>.
    /// Reserved for the air-gap follow-up; not enforced in v1.
    /// </summary>
    public TimeSpan? MaxAcceptableSigningAge { get; init; }

    /// <summary>
    /// When <see langword="true"/> (default), JSON files MUST contain a <c>signature.expiration</c>
    /// field (or top-level <c>expiration</c>) and the value must be in the future. See the dotnetup
    /// signature-verification doc §9.
    /// </summary>
    public bool RequireJsonExpirationField { get; init; } = true;

    public SignatureVerificationOptions(
        X509Certificate2Collection trustedCodeSigningRoots,
        X509Certificate2Collection trustedTimestampRoots)
    {
        TrustedCodeSigningRoots = trustedCodeSigningRoots ?? throw new ArgumentNullException(nameof(trustedCodeSigningRoots));
        TrustedTimestampRoots = trustedTimestampRoots ?? throw new ArgumentNullException(nameof(trustedTimestampRoots));
    }
}

/// <summary>Revocation-checking policy for signature verification.</summary>
internal enum RevocationCheckMode
{
    /// <summary>CRL/OCSP must succeed. Fail closed if unreachable. Recommended default.</summary>
    Online,

    /// <summary>Use locally cached CRLs only (<see cref="System.Security.Cryptography.X509Certificates.X509RevocationMode.Offline"/>).</summary>
    Offline,

    /// <summary>Skip revocation entirely. Should be paired with <see cref="SignatureVerificationOptions.MaxAcceptableSigningAge"/>.</summary>
    NoCheck,
}

/// <summary>Controls how aggressively <see cref="SignatureVerifier.Verify"/> runs its checks.</summary>
internal enum VerificationMode
{
    /// <summary>
    /// Stop on the first real failure (skips never trigger this). Production default —
    /// callers only need to know whether verification succeeded, and skipping later checks
    /// shaves CPU and avoids a second chain build on what's already a known-bad signature.
    /// </summary>
    ShortCircuit,

    /// <summary>
    /// Run every check and report every failure. Useful for diagnostics, signing producers,
    /// and tests that want to assert spec coverage in one run.
    /// </summary>
    CollectAll,
}
