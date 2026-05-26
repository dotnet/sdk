// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

// OIDs the BCL recognises but we deliberately do NOT accept. Maintained next to (rather
// than baked into) the allow-list so a future BCL-drift detector can subtract these
// before reporting "new OID, please add" — preventing false positives for OIDs we
// chose to exclude on purpose.
internal static partial class SignatureVerifier
{
    // Pure-ECDSA, DSA, Ed25519, Ed448 — see §4 of signature-verification.md. Composite
    // ML-DSA variants whose second component is ECDSA / EdDSA remain accepted via
    // s_pqcPureKeyOids: those are PQ hybrid schemes, not stand-alone classical sigs.
    internal static readonly HashSet<string> s_explicitlyUnsupportedSignatureKeyOids = new(StringComparer.Ordinal)
    {
        "1.2.840.10045.2.1",  // id-ecPublicKey / pure ECDSA (RFC 5480 §2.1.1)
        "1.2.840.10040.4.1",  // id-dsa (RFC 3279 §2.3.2)
        "1.3.101.112",        // id-Ed25519 (RFC 8410)
        "1.3.101.113",        // id-Ed448   (RFC 8410)
        // Pre-hash PQC variants are tracked separately in s_pqcPreHashOids
        // (SignatureVerifier.PqcOids.cs) — they have their own SPKI-vs-signature
        // distinction and the spec calls them out individually.
    };

    // Collision-broken hash functions plus families the BCL recognises but the .NET Release
    // pipeline does not use. Rejected per §4 of signature-verification.md.
    internal static readonly HashSet<string> s_explicitlyUnsupportedDigestOids = new(StringComparer.Ordinal)
    {
        // --- Collision-broken (must reject) ---
        "1.3.14.3.2.26",       // id-sha1 (RFC 3279 §2.1)
        "1.2.840.113549.2.5",  // md5     (RFC 1321 / RFC 3279 §2.1)
        "1.2.840.113549.2.2",  // md2     (RFC 1319)
        "1.3.14.3.2.18",       // id-sha  (legacy SHA-0) — never standardised

        // Unused by .NET Release (drop to shrink attack surface) ---
        // SHA-3 family (NIST CSOR; RFC 8702 §2). The .NET Release pipeline uses SHA-2.
        "2.16.840.1.101.3.4.2.8",   // id-sha3-256
        "2.16.840.1.101.3.4.2.9",   // id-sha3-384
        "2.16.840.1.101.3.4.2.10",  // id-sha3-512

        // SHAKE-128 / SHAKE-256 (FIPS 202 XOFs; RFC 8702). Not PQ algorithms themselves —
        // PQ schemes that use SHAKE internally carry their own combined OIDs (pure-mode
        // SLH-DSA-SHAKE-* in s_pqcPureKeyOids, pre-hash HashSLH-DSA-SHAKE-* in
        // s_pqcPreHashOids). Standalone id-shake128 / id-shake256 as a CMS digestAlgorithm
        // would only matter for a non-PQ signer electing to use SHAKE classically, which
        // the .NET Release pipeline does not do.
        "2.16.840.1.101.3.4.2.11",  // id-shake128
        "2.16.840.1.101.3.4.2.12",  // id-shake256
    };
}
