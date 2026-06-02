// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

// Post-quantum signature algorithm OIDs supported by .NET 11's MLDsa / SlhDsa / CompositeMLDsa BCL types.
//
// The list is split in two:
//   * s_pqcPureKeyOids   — pure-mode OIDs that may legitimately appear in BOTH a
//     certificate's SubjectPublicKeyInfo.AlgorithmIdentifier AND in CMS
//     SignerInfo.DigestAlgorithm (per draft-ietf-lamps-cms-ml-dsa, the pure
//     signature scheme does its own internal hashing so the alg-id repeats).
//   * s_pqcPreHashOids   — pre-hash variants (HashML-DSA per FIPS 204 §5.4,
//     HashSLH-DSA per FIPS 205 §10.2). The IETF PKIX profile drafts restrict
//     these to per-signature use in SignerInfo.signatureAlgorithm and forbid
//     them in a certificate's SubjectPublicKeyInfo:
//       * draft-ietf-lamps-dilithium-certificates §1 / §8.3 ("Only [pure ML-DSA]
//         is specified in this document.")
//       * draft-ietf-lamps-x509-slhdsa (same restriction for SLH-DSA)
//     so they are signature/digest-only and MUST NOT appear in SPKI.
//
// s_pqcSignatureOids below is the union of the two and is kept as the drift-detection
// target for the BCL's KnownOids arrays (see PqcOidList_StaysInSyncWithBcl).
//
// Source of truth for the OID list:
// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Security/Cryptography/Oids.cs
internal static partial class SignatureVerifier
{
    private static readonly HashSet<string> s_pqcPureKeyOids =
    [
        // === FIPS 204 ML-DSA (pure mode) — NIST CSOR 2.16.840.1.101.3.4.3.17–19 ===
        "2.16.840.1.101.3.4.3.17", // ML-DSA-44
        "2.16.840.1.101.3.4.3.18", // ML-DSA-65
        "2.16.840.1.101.3.4.3.19", // ML-DSA-87

        // === FIPS 205 SLH-DSA (pure mode) — .20–.31 ===
        "2.16.840.1.101.3.4.3.20", // SLH-DSA-SHA2-128s
        "2.16.840.1.101.3.4.3.21", // SLH-DSA-SHA2-128f
        "2.16.840.1.101.3.4.3.22", // SLH-DSA-SHA2-192s
        "2.16.840.1.101.3.4.3.23", // SLH-DSA-SHA2-192f
        "2.16.840.1.101.3.4.3.24", // SLH-DSA-SHA2-256s
        "2.16.840.1.101.3.4.3.25", // SLH-DSA-SHA2-256f
        "2.16.840.1.101.3.4.3.26", // SLH-DSA-SHAKE-128s
        "2.16.840.1.101.3.4.3.27", // SLH-DSA-SHAKE-128f
        "2.16.840.1.101.3.4.3.28", // SLH-DSA-SHAKE-192s
        "2.16.840.1.101.3.4.3.29", // SLH-DSA-SHAKE-192f
        "2.16.840.1.101.3.4.3.30", // SLH-DSA-SHAKE-256s
        "2.16.840.1.101.3.4.3.31", // SLH-DSA-SHAKE-256f

        // === Composite ML-DSA (draft-ietf-lamps-pq-composite-sigs, 1.3.6.1.5.5.7.6.37–54) ===
        // Hybrid algorithms combining ML-DSA with a traditional algorithm so a relying
        // party that has only verified ONE of the two component algorithms still gets some
        // security; the intended deployment vehicle during the PQC transition.
        "1.3.6.1.5.5.7.6.37", // MLDSA44-RSA2048-PSS-SHA256
        "1.3.6.1.5.5.7.6.38", // MLDSA44-RSA2048-PKCS15-SHA256
        "1.3.6.1.5.5.7.6.39", // MLDSA44-Ed25519-SHA512
        "1.3.6.1.5.5.7.6.40", // MLDSA44-ECDSA-P256-SHA256
        "1.3.6.1.5.5.7.6.41", // MLDSA65-RSA3072-PSS-SHA512
        "1.3.6.1.5.5.7.6.42", // MLDSA65-RSA3072-PKCS15-SHA512
        "1.3.6.1.5.5.7.6.43", // MLDSA65-RSA4096-PSS-SHA512
        "1.3.6.1.5.5.7.6.44", // MLDSA65-RSA4096-PKCS15-SHA512
        "1.3.6.1.5.5.7.6.45", // MLDSA65-ECDSA-P256-SHA512
        "1.3.6.1.5.5.7.6.46", // MLDSA65-ECDSA-P384-SHA512
        "1.3.6.1.5.5.7.6.47", // MLDSA65-ECDSA-brainpoolP256r1-SHA512
        "1.3.6.1.5.5.7.6.48", // MLDSA65-Ed25519-SHA512
        "1.3.6.1.5.5.7.6.49", // MLDSA87-ECDSA-P384-SHA512
        "1.3.6.1.5.5.7.6.50", // MLDSA87-ECDSA-brainpoolP384r1-SHA512
        "1.3.6.1.5.5.7.6.51", // MLDSA87-Ed448-SHAKE256
        "1.3.6.1.5.5.7.6.52", // MLDSA87-RSA3072-PSS-SHA512
        "1.3.6.1.5.5.7.6.53", // MLDSA87-RSA4096-PSS-SHA512
        "1.3.6.1.5.5.7.6.54", // MLDSA87-ECDSA-P521-SHA512
    ];

    private static readonly HashSet<string> s_pqcPreHashOids =
    [
        // === Pre-hash ML-DSA (.32–.34) ===
        "2.16.840.1.101.3.4.3.32", // HashML-DSA-44-SHA512
        "2.16.840.1.101.3.4.3.33", // HashML-DSA-65-SHA512
        "2.16.840.1.101.3.4.3.34", // HashML-DSA-87-SHA512

        // === Pre-hash SLH-DSA (.35–.46) ===
        "2.16.840.1.101.3.4.3.35", // HashSLH-DSA-SHA2-128s-SHA256
        "2.16.840.1.101.3.4.3.36", // HashSLH-DSA-SHA2-128f-SHA256
        "2.16.840.1.101.3.4.3.37", // HashSLH-DSA-SHA2-192s-SHA512
        "2.16.840.1.101.3.4.3.38", // HashSLH-DSA-SHA2-192f-SHA512
        "2.16.840.1.101.3.4.3.39", // HashSLH-DSA-SHA2-256s-SHA512
        "2.16.840.1.101.3.4.3.40", // HashSLH-DSA-SHA2-256f-SHA512
        "2.16.840.1.101.3.4.3.41", // HashSLH-DSA-SHAKE-128s-SHAKE128
        "2.16.840.1.101.3.4.3.42", // HashSLH-DSA-SHAKE-128f-SHAKE128
        "2.16.840.1.101.3.4.3.43", // HashSLH-DSA-SHAKE-192s-SHAKE256
        "2.16.840.1.101.3.4.3.44", // HashSLH-DSA-SHAKE-192f-SHAKE256
        "2.16.840.1.101.3.4.3.45", // HashSLH-DSA-SHAKE-256s-SHAKE256
        "2.16.840.1.101.3.4.3.46", // HashSLH-DSA-SHAKE-256f-SHAKE256
    ];

    // Union of pure + pre-hash. Exposed (internal) only as the drift-detection target
    // against the BCL's MLDsa.KnownOids / SlhDsa.s_knownOids / CompositeMLDsa.s_knownOids
    // arrays (see PqcOidList_StaysInSyncWithBcl in the test project).
    internal static readonly HashSet<string> s_pqcSignatureOids =
    [
        .. s_pqcPureKeyOids,
        .. s_pqcPreHashOids,
    ];
}
