// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

// SHA-2 digest-algorithm OIDs (NIST CSOR arc 2.16.840.1.101.3.4.2; SHA-2 registered
// in RFC 5754 §2). Deliberately scoped to SHA-2 only: SHA-3 and SHAKE are recognised
// by the BCL but are not used by the .NET Release pipeline — see
// s_explicitlyUnsupportedDigestOids in SignatureVerifier.UnsupportedOids.cs.
// SHA-1 / MD5 are also rejected via the same explicitly-unsupported set.
// The composite allow-list used at verification time (s_allowedDigestOids) also
// includes the pure-PQC OIDs from s_pqcSignatureOids — see the static constructor in
// SignatureVerifier.cs for the union, and the comment on s_pqcSignatureOids for why
// pure-PQC OIDs are valid CMS SignerInfo.DigestAlgorithm values.
// https://datatracker.ietf.org/doc/html/rfc5754#section-2
internal static partial class SignatureVerifier
{
    private const string OidIdSha256 = "2.16.840.1.101.3.4.2.1";  // id-sha256 (RFC 5754 §2) — legacy v2 manifests still use this
    private const string OidIdSha384 = "2.16.840.1.101.3.4.2.2";  // id-sha384 (RFC 5754 §2) — current v3 production digest
    private const string OidIdSha512 = "2.16.840.1.101.3.4.2.3";  // id-sha512 (RFC 5754 §2) — reserved as a future rotation target
}
