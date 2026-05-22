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
    /// Reserved for an air-gap follow-up; not enforced in v1.
    ///
    /// <para>
    /// When implemented, this is NOT a "grace period after which fresh signatures stop being
    /// re-checked" — it is the inverse: a hard ceiling on how old a TSA-attested signing time
    /// is allowed to be when <see cref="RevocationMode"/> is not
    /// <see cref="RevocationCheckMode.Online"/>. The verifier will reject any TSA timestamp
    /// older than <c>UtcNow - MaxAcceptableSigningAge</c>, regardless of how recently the
    /// caller re-ran verification. The intent is to bound trust on offline / air-gapped
    /// installs where revocation cannot be checked at consumption time: an old enough
    /// manifest, even if its TSA chain still parses, is presumed stale.
    /// </para>
    ///
    /// <para>
    /// Ignored when <see cref="RevocationMode"/> is <see cref="RevocationCheckMode.Online"/>
    /// (CRL/OCSP is the authoritative freshness signal in that mode).
    /// </para>
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
    /// Stop on the first recorded entry — failure or <see cref="FailureCode.CheckSkipped"/>.
    /// Production default: callers only need to know whether verification succeeded, and
    /// skipping later checks shaves CPU and avoids a second chain build on what's already a
    /// known-bad signature. Skips are appended to the same <c>Failures</c> list as real
    /// failures (a skip only ever appears as a downstream consequence of an upstream
    /// failure), so they invalidate the result and trip short-circuit just like a failure.
    /// See <see cref="VerificationResult.AddSkip"/> for the rationale.
    /// </summary>
    ShortCircuit,

    /// <summary>
    /// Run every check and report every failure. Useful for diagnostics, signing producers,
    /// and tests that want to assert spec coverage in one run.
    /// </summary>
    CollectAll,
}
