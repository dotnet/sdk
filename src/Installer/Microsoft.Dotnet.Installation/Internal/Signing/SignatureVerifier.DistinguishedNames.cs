// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

internal static partial class SignatureVerifier
{
    // Required signer subject DN (RDN set; OID-based, order-insensitive). Spec §5.1.
    // internal: tests mint cert chains with this exact DN to drive chain-build behavior
    // (EKU constraint, validity window) without tripping the DN pin first.
    internal static readonly (string Oid, string Value)[] s_requiredSubjectRdns =
    [
        ("2.5.4.3",  "Microsoft Corporation"), // CN
        ("2.5.4.11", ".NET Release"),          // OU
        ("2.5.4.10", "Microsoft Corporation"), // O
        ("2.5.4.7",  "Redmond"),               // L
        ("2.5.4.8",  "Washington"),            // S
        ("2.5.4.6",  "US"),                    // C
    ];

    // Required signer issuer DN (DigiCert code-signing intermediate). Spec §5.2.
    // internal: see s_requiredSubjectRdns.
    internal static readonly (string Oid, string Value)[] s_requiredIssuerRdns =
    [
        ("2.5.4.3",  "DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1"),
        ("2.5.4.10", "DigiCert, Inc."),
        ("2.5.4.6",  "US"),
    ];

    // Required TSA-cert immediate-issuer DN (DigiCert timestamping intermediate). Spec §7.
    // Defense-in-depth: tightens the TSA chain beyond "any cert chaining to a root in
    // timestampctl.pem" by also pinning the intermediate that issued the TSA leaf, mirroring
    // the code-signing intermediate pin in §5.2. The CN intentionally differs from
    // s_requiredIssuerRdns above (timestamping vs. code-signing intermediates are distinct
    // certs in the DigiCert hierarchy) — the visual similarity is not duplication.
    private static readonly (string Oid, string Value)[] s_requiredTimestampIssuerRdns =
    [
        ("2.5.4.3",  "DigiCert Trusted G4 TimeStamping RSA4096 SHA256 2025 CA1"),
        ("2.5.4.10", "DigiCert, Inc."),
        ("2.5.4.6",  "US"),
    ];
}
