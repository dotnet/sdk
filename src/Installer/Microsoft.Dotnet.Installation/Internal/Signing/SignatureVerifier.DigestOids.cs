// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

// SHA-2 / SHA-3 / SHAKE digest-algorithm OIDs registered under NIST CSOR
// 2.16.840.1.101.3.4.2. Permitted as values of CMS SignerInfo.DigestAlgorithm.
// SHA-1 and MD5 are deliberately omitted.
//
// References:
//   * SHA-2 OID registrations:  RFC 5754 §2  — https://datatracker.ietf.org/doc/html/rfc5754#section-2
//   * SHA-3 / SHAKE registrations: RFC 8702 §2 — https://datatracker.ietf.org/doc/html/rfc8702#section-2
//   * SHAKE-with-ECDSA usage:      RFC 8692     — https://datatracker.ietf.org/doc/html/rfc8692
internal static partial class SignatureVerifier
{
    private const string OidIdSha256   = "2.16.840.1.101.3.4.2.1";
    private const string OidIdSha384   = "2.16.840.1.101.3.4.2.2";
    private const string OidIdSha512   = "2.16.840.1.101.3.4.2.3";
    private const string OidIdSha3_256 = "2.16.840.1.101.3.4.2.8";
    private const string OidIdSha3_384 = "2.16.840.1.101.3.4.2.9";
    private const string OidIdSha3_512 = "2.16.840.1.101.3.4.2.10";
    // id-shake128 is also used by ECDSA-with-SHAKE per RFC 8692 and by pure
    // ML-DSA / SLH-DSA-SHAKE variants (which do internal hashing).
    private const string OidIdShake128 = "2.16.840.1.101.3.4.2.11";
    private const string OidIdShake256 = "2.16.840.1.101.3.4.2.12";

    // Digest OIDs permitted in CMS SignerInfo.DigestAlgorithm.
    //
    // The pure-PQC OIDs in s_pqcSignatureOids are ALSO valid here because pure
    // ML-DSA / SLH-DSA / Composite ML-DSA do their own internal hashing, and CMS
    // encodes the algorithm-identifier in both SignatureAlgorithm and DigestAlgorithm
    // (see the comment on s_pqcSignatureOids in SignatureVerifier.cs). They are
    // unioned in via the collection-expression spread below.
    private static readonly HashSet<string> s_allowedDigestOids =
    [
        OidIdSha256,
        OidIdSha384,
        OidIdSha512,
        OidIdSha3_256,
        OidIdSha3_384,
        OidIdSha3_512,
        OidIdShake128,
        OidIdShake256,
        .. s_pqcSignatureOids,
    ];
}
