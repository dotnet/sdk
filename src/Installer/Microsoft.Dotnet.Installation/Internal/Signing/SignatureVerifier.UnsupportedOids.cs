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

    // Collision-broken hash functions; rejected per §4 of signature-verification.md.
    internal static readonly HashSet<string> s_explicitlyUnsupportedDigestOids = new(StringComparer.Ordinal)
    {
        "1.3.14.3.2.26",       // id-sha1 (RFC 3279 §2.1)
        "1.2.840.113549.2.5",  // md5     (RFC 1321 / RFC 3279 §2.1)
        "1.2.840.113549.2.2",  // md2     (RFC 1319)
        "1.3.14.3.2.18",       // id-sha  (legacy SHA-0) — never standardised
    };
}
