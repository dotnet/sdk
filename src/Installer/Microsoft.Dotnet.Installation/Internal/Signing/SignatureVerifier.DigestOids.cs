// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

// SHA-2, SHA-3, and SHAKE digest-algorithm OIDs (NIST CSOR arc 2.16.840.1.101.3.4.2;
// SHA-2 registered in RFC 5754 §2, SHA-3 / SHAKE in RFC 8702 §2). SHA-1 / MD5 deliberately
// omitted. The composite allow-list used at verification time (s_allowedDigestOids) also
// includes the pure-PQC OIDs from s_pqcSignatureOids — see the static constructor in
// SignatureVerifier.cs for the union, and the comment on s_pqcSignatureOids for why
// pure-PQC OIDs are valid CMS SignerInfo.DigestAlgorithm values.
// https://datatracker.ietf.org/doc/html/rfc5754#section-2
// https://datatracker.ietf.org/doc/html/rfc8702#section-2
internal static partial class SignatureVerifier
{
    private const string OidIdSha256 = "2.16.840.1.101.3.4.2.1";  // id-sha256  (RFC 5754 §2)
    private const string OidIdSha384 = "2.16.840.1.101.3.4.2.2";  // id-sha384  (RFC 5754 §2)
    private const string OidIdSha512 = "2.16.840.1.101.3.4.2.3";  // id-sha512  (RFC 5754 §2)
    private const string OidIdSha3_256 = "2.16.840.1.101.3.4.2.8";  // id-sha3-256 (RFC 8702 §2)
    private const string OidIdSha3_384 = "2.16.840.1.101.3.4.2.9";  // id-sha3-384 (RFC 8702 §2)
    private const string OidIdSha3_512 = "2.16.840.1.101.3.4.2.10"; // id-sha3-512 (RFC 8702 §2)
    private const string OidIdShake128 = "2.16.840.1.101.3.4.2.11"; // id-shake128 (RFC 8702; used by ECDSA-with-SHAKE per RFC 8692 and by pure ML-DSA / SLH-DSA-SHAKE variants)
    private const string OidIdShake256 = "2.16.840.1.101.3.4.2.12"; // id-shake256 (RFC 8702 §2)
}
