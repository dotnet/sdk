// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

internal static partial class SignatureVerifier
{
    private const string EkuCodeSigning = "1.3.6.1.5.5.7.3.3";   // id-kp-codeSigning (RFC 5280 §4.2.1.12) https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.12
    private const string EkuTimeStamping = "1.3.6.1.5.5.7.3.8";  // id-kp-timeStamping (RFC 5280 §4.2.1.12) https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.12
    private const string EkuAnyExtended = "2.5.29.37.0";         // anyExtendedKeyUsage (RFC 5280 §4.2.1.12) https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.12
    private const string EkuServerAuth = "1.3.6.1.5.5.7.3.1";    // id-kp-serverAuth (RFC 5280 §4.2.1.12)

    // CA/Browser Forum Code Signing BR §7.1.2.3(f) explicitly permits these EKUs alongside
    // id-kp-codeSigning on a code-signing leaf. Listing them here lets a CAB-Forum-conformant
    // .NET Release cert carry them in addition to id-kp-codeSigning without being rejected as
    // "non-exclusive". Note: this profile applies ONLY to the primary code-signing cert; the
    // TSA cert keeps strict EKU exclusivity per RFC 3161 §2.3 (see EvaluateTimestampEku).
    private const string EkuLifetimeSigning = "1.3.6.1.4.1.311.10.3.13";  // Microsoft Authenticode Lifetime Signing
    private const string EkuEmailProtection = "1.3.6.1.5.5.7.3.4";        // id-kp-emailProtection (RFC 5280 §4.2.1.12)
    private const string EkuDocumentSigning = "1.3.6.1.4.1.311.3.10.3.12"; // Microsoft Document Signing

    private static readonly HashSet<string> s_codeSigningPermittedEkus =
    [
        EkuCodeSigning,
        EkuLifetimeSigning,
        EkuEmailProtection,
        EkuDocumentSigning,
    ];
}
