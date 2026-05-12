// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// CMS detached-signature verifier for .NET release manifest JSON files.
/// Ported from <c>detached_signature_validation/src/DotnetSignatureVerifier/SignatureVerifier.cs</c>.
/// Implements the spec at <c>detached_signature_validation/signature_requirements.md</c>.
/// Differences from the standalone tool:
/// <list type="bullet">
///   <item>Takes <see cref="byte"/>[] inputs instead of file paths.</item>
///   <item>Receives trusted roots from <see cref="SignatureVerificationOptions"/> instead of loading PEMs from disk.</item>
///   <item>JSON expiration check runs whenever <see cref="SignatureVerificationOptions.RequireJsonExpirationField"/>
///         is <see langword="true"/> (no file-extension probe).</item>
///   <item><c>RevocationMode</c> is configurable via <see cref="SignatureVerificationOptions.RevocationMode"/>;
///         the standalone tool hardcodes <c>Online</c>.</item>
/// </list>
/// </summary>
internal static class SignatureVerifier
{
    // Required signer subject DN (RDN set; OID-based, order-insensitive). Spec §5.1.
    private static readonly (string Oid, string Value)[] RequiredSubjectRdns =
    {
        ("2.5.4.3",  "Microsoft Corporation"), // CN
        ("2.5.4.11", ".NET Release"),          // OU
        ("2.5.4.10", "Microsoft Corporation"), // O
        ("2.5.4.7",  "Redmond"),               // L
        ("2.5.4.8",  "Washington"),            // S
        ("2.5.4.6",  "US"),                    // C
    };

    // Required signer issuer DN (DigiCert code-signing intermediate). Spec §5.2.
    private static readonly (string Oid, string Value)[] RequiredIssuerRdns =
    {
        ("2.5.4.3",  "DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1"),
        ("2.5.4.10", "DigiCert, Inc."),
        ("2.5.4.6",  "US"),
    };

    private const string EkuCodeSigning = "1.3.6.1.5.5.7.3.3";   // id-kp-codeSigning
    private const string EkuTimeStamping = "1.3.6.1.5.5.7.3.8";  // id-kp-timeStamping
    private const string EkuAnyExtended = "2.5.29.37.0";         // anyExtendedKeyUsage

    private const string OidSigningTime = "1.2.840.113549.1.9.5";
    private const string OidContentTypeAttr = "1.2.840.113549.1.9.3";
    private const string OidMessageDigestAttr = "1.2.840.113549.1.9.4";
    private const string OidIdData = "1.2.840.113549.1.7.1";
    private const string OidTimestampToken = "1.2.840.113549.1.9.16.2.14";

    private const string OidRsa = "1.2.840.113549.1.1.1";
    private const string OidEcdsa = "1.2.840.10045.2.1";

    private static readonly HashSet<string> AllowedDigestOids = new(StringComparer.Ordinal)
    {
        "2.16.840.1.101.3.4.2.1", // SHA-256
        "2.16.840.1.101.3.4.2.2", // SHA-384
        "2.16.840.1.101.3.4.2.3", // SHA-512
    };

    private static readonly TimeSpan SigningTimeTolerance = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RevocationRetrievalTimeout = TimeSpan.FromSeconds(30);

    // The OS root store (StoreName.Root for both CurrentUser and LocalMachine) is read into
    // every X509Chain we build. Opening the OS store on each call costs 50–200ms on Windows;
    // we build two chains per Verify (primary + TSA) so that's 4 store opens per call.
    // Cache the union once per process — chain build is read-only.
    private static readonly Lazy<X509Certificate2Collection> s_osRoots = new(LoadOsRoots, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Verifies <paramref name="content"/> against the detached CMS signature in <paramref name="signature"/>.
    /// Collects every spec violation it can detect; the result is valid only when zero failures are recorded.
    /// </summary>
    /// <param name="content">Bytes of the signed JSON document.</param>
    /// <param name="signature">Bytes of the detached CMS / PKCS#7 SignedData blob (.p7s).</param>
    /// <param name="options">Trust anchors and policy knobs.</param>
    /// <param name="nowOverride">When set, used in place of <see cref="DateTimeOffset.UtcNow"/> for tests.</param>
    public static VerificationResult Verify(
        byte[] content,
        byte[] signature,
        SignatureVerificationOptions options,
        DateTimeOffset? nowOverride = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(options);

        var result = new VerificationResult();

        X509Certificate2Collection codesignRoots = options.TrustedCodeSigningRoots;
        X509Certificate2Collection timestampRoots = options.TrustedTimestampRoots;

        if (codesignRoots.Count == 0)
            result.Add(FailureCode.TrustedRootsEmpty, "TrustedCodeSigningRoots is empty.");
        if (timestampRoots.Count == 0)
            result.Add(FailureCode.TrustedRootsEmpty, "TrustedTimestampRoots is empty.");

        // ---- CMS decode ----
        SignedCms? cms = new SignedCms(new ContentInfo(new Oid(OidIdData), content), detached: true);
        SignerInfo? signer = null;
        X509Certificate2? signerCert = null;

        try
        {
            cms.Decode(signature);
        }
        catch (CryptographicException ex)
        {
            result.Add(FailureCode.SigDecodeFailed, $"Signature is not a valid PKCS#7/CMS SignedData blob: {ex.Message}");
            cms = null;
        }

        if (cms is not null)
        {
            if (cms.ContentInfo.ContentType.Value != OidIdData)
                result.Add(FailureCode.SigNotCms, $"Unexpected encapsulated content-type OID {cms.ContentInfo.ContentType.Value}; expected id-data ({OidIdData}).");

            if (cms.SignerInfos.Count != 1)
                result.Add(FailureCode.SigMultipleSigners, $"Expected exactly one signer; found {cms.SignerInfos.Count}.");

            if (cms.SignerInfos.Count >= 1)
            {
                signer = cms.SignerInfos[0];
                signerCert = signer.Certificate;
                if (signerCert is null)
                    result.Add(FailureCode.SignerCertMissing, "SignerInfo does not carry the signer certificate.");
            }

            // ---- Cryptographic integrity ----
            try
            {
                cms.CheckSignature(verifySignatureOnly: true);
            }
            catch (CryptographicException ex)
            {
                result.Add(FailureCode.SigCryptoInvalid, $"CMS cryptographic verification failed: {ex.Message}");
            }
        }

        // ---- Algorithm policy ----
        if (signer is not null)
        {
            string digestOid = signer.DigestAlgorithm.Value ?? string.Empty;
            if (!AllowedDigestOids.Contains(digestOid))
                result.Add(FailureCode.WeakDigest, $"Digest algorithm OID {digestOid} is not permitted.");
        }
        else
        {
            result.AddSkip("Digest algorithm policy not evaluated: no SignerInfo available.");
        }

        if (signerCert is not null)
        {
            string keyOid = signerCert.PublicKey.Oid.Value ?? string.Empty;
            if (keyOid != OidRsa && keyOid != OidEcdsa)
                result.Add(FailureCode.WeakSignatureAlgorithm, $"Signer public-key algorithm OID {keyOid} is not permitted (RSA or ECDSA required).");
        }
        else
        {
            result.AddSkip("Public-key algorithm policy not evaluated: no signer certificate available.");
        }

        // ---- Subject DN ----
        if (signerCert is not null)
        {
            if (!DistinguishedNameMatches(signerCert.SubjectName, RequiredSubjectRdns, "Subject", out string subjectDetail))
                result.Add(FailureCode.SubjectMismatch, subjectDetail);
        }
        else
        {
            result.AddSkip("Subject DN not evaluated: no signer certificate available.");
        }

        // ---- Issuer DN ----
        if (signerCert is not null)
        {
            if (!DistinguishedNameMatches(signerCert.IssuerName, RequiredIssuerRdns, "Issuer", out string issuerDetail))
                result.Add(FailureCode.IssuerMismatch, issuerDetail);
        }
        else
        {
            result.AddSkip("Issuer DN not evaluated: no signer certificate available.");
        }

        // ---- EKU (primary) ----
        if (signerCert is not null)
            EvaluateEku(signerCert, EkuCodeSigning, primary: true, result);
        else
            result.AddSkip("Primary EKU not evaluated: no signer certificate available.");

        // ---- Signed attributes (optional under PKCS#7) ----
        DateTime? claimedSigningTimeUtc = null;
        if (signer is not null && signer.SignedAttributes.Count > 0)
        {
            EvaluateContentTypeAttribute(signer, result);
            EvaluateMessageDigestAttribute(signer, result);
            claimedSigningTimeUtc = TryReadSigningTimeAttribute(signer, result);
        }

        // ---- Timestamp ----
        DateTimeOffset? tsaTimestampUtc = null;
        SignedCms? tsaCms = null;
        X509Certificate2? tsaCert = null;
        if (signer is not null)
        {
            (tsaTimestampUtc, tsaCms, tsaCert) = EvaluateTimestamp(signer, claimedSigningTimeUtc, result);
        }
        else
        {
            result.AddSkip("Timestamp not evaluated: no SignerInfo available.");
        }

        // ---- TSA chain ----
        if (tsaCert is not null && tsaCms is not null && tsaTimestampUtc is not null)
        {
            EvaluateEku(tsaCert, EkuTimeStamping, primary: false, result);
            EvaluateChain(
                tsaCert,
                extraStore: tsaCms.Certificates,
                customRoots: timestampRoots,
                verificationTime: tsaTimestampUtc.Value.UtcDateTime,
                revocationMode: options.RevocationMode,
                timeoutCode: FailureCode.TimestampRevocationUnavailable,
                revokedCode: FailureCode.TimestampChainFailed,
                genericCode: FailureCode.TimestampChainFailed,
                role: "timestamp authority",
                result);
        }
        else if (signer is not null)
        {
            result.AddSkip("Timestamp chain not evaluated: timestamp could not be extracted.");
        }

        // ---- Primary chain ----
        if (signerCert is not null && cms is not null)
        {
            DateTime verificationTime = tsaTimestampUtc?.UtcDateTime ?? (nowOverride ?? DateTimeOffset.UtcNow).UtcDateTime;
            if (tsaTimestampUtc is null)
                result.AddSkip($"Primary chain verification time fell back to current UTC ({verificationTime:O}) because no TSA timestamp was available.");

            EvaluateChain(
                signerCert,
                extraStore: cms.Certificates,
                customRoots: codesignRoots,
                verificationTime: verificationTime,
                revocationMode: options.RevocationMode,
                timeoutCode: FailureCode.RevocationUnavailable,
                revokedCode: FailureCode.Revoked,
                genericCode: FailureCode.ChainBuildFailed,
                role: "primary signer",
                result);
        }
        else
        {
            result.AddSkip("Primary chain not evaluated: signer certificate or CMS missing.");
        }

        // ---- JSON expiration policy ----
        if (options.RequireJsonExpirationField)
        {
            if (tsaTimestampUtc is null)
            {
                result.AddSkip("JSON expiration policy not evaluated: no TSA timestamp.");
            }
            else
            {
                EvaluateJsonExpiration(content, tsaTimestampUtc.Value, nowOverride ?? DateTimeOffset.UtcNow, result);
            }
        }

        return result;
    }

    // ---------------- Helpers ----------------

    private static bool DistinguishedNameMatches(
        X500DistinguishedName dn,
        (string Oid, string Value)[] required,
        string label,
        out string detail)
    {
        var actual = new List<(string Oid, string Value)>();
        foreach (X500RelativeDistinguishedName rdn in dn.EnumerateRelativeDistinguishedNames())
        {
            Oid? type;
            string? value;
            try
            {
                type = rdn.GetSingleElementType();
                value = rdn.GetSingleElementValue();
            }
            catch (InvalidOperationException)
            {
                detail = $"{label} contains a multi-valued RDN which is not permitted. {label}={dn.Name}";
                return false;
            }

            if (type?.Value is null || value is null)
            {
                detail = $"{label} contains an unparseable RDN. {label}={dn.Name}";
                return false;
            }
            actual.Add((type.Value, value));
        }

        if (actual.Count != required.Length)
        {
            detail = $"{label} has {actual.Count} RDNs; expected exactly {required.Length}. {label}={dn.Name}";
            return false;
        }

        foreach (var req in required)
        {
            int idx = actual.FindIndex(a =>
                string.Equals(a.Oid, req.Oid, StringComparison.Ordinal) &&
                string.Equals(a.Value, req.Value, StringComparison.Ordinal));
            if (idx < 0)
            {
                detail = $"{label} missing required RDN {req.Oid}={req.Value}. {label}={dn.Name}";
                return false;
            }
            actual.RemoveAt(idx);
        }

        detail = string.Empty;
        return true;
    }

    private static void EvaluateEku(X509Certificate2 cert, string requiredOid, bool primary, VerificationResult result)
    {
        X509EnhancedKeyUsageExtension? eku = null;
        foreach (X509Extension ext in cert.Extensions)
        {
            if (ext is X509EnhancedKeyUsageExtension e)
            {
                eku = e;
                break;
            }
        }

        if (eku is null)
        {
            result.Add(
                primary ? FailureCode.EkuMissing : FailureCode.TimestampEkuInvalid,
                $"Certificate {cert.Subject} has no Extended Key Usage extension.");
            return;
        }

        var oids = new List<string>(eku.EnhancedKeyUsages.Count);
        foreach (Oid o in eku.EnhancedKeyUsages)
        {
            if (o.Value is null) continue;
            oids.Add(o.Value);
        }

        if (oids.Count != 1 || oids[0] != requiredOid || oids.Contains(EkuAnyExtended))
        {
            result.Add(
                primary ? FailureCode.EkuNotExclusiveCodeSign : FailureCode.TimestampEkuInvalid,
                $"Certificate EKU must contain exactly {requiredOid} and nothing else. Found: [{string.Join(", ", oids)}].");
        }
    }

    private static bool TryGetSignedAttribute(SignerInfo signer, string oid, out AsnEncodedData? data)
    {
        foreach (CryptographicAttributeObject attr in signer.SignedAttributes)
        {
            if (attr.Oid.Value == oid && attr.Values.Count > 0)
            {
                data = attr.Values[0];
                return true;
            }
        }
        data = null;
        return false;
    }

    private static bool TryGetUnsignedAttribute(SignerInfo signer, string oid, out AsnEncodedData? data)
    {
        foreach (CryptographicAttributeObject attr in signer.UnsignedAttributes)
        {
            if (attr.Oid.Value == oid && attr.Values.Count > 0)
            {
                data = attr.Values[0];
                return true;
            }
        }
        data = null;
        return false;
    }

    private static void EvaluateContentTypeAttribute(SignerInfo signer, VerificationResult result)
    {
        if (!TryGetSignedAttribute(signer, OidContentTypeAttr, out AsnEncodedData? ctData) || ctData is null)
        {
            result.Add(FailureCode.ContentTypeAttributeInvalid, "Missing signed content-type attribute.");
            return;
        }
        try
        {
            string ctOid = AsnDecoder.ReadObjectIdentifier(ctData.RawData, AsnEncodingRules.DER, out _);
            if (ctOid != OidIdData)
                result.Add(FailureCode.ContentTypeAttributeInvalid, $"Signed content-type is {ctOid}; expected id-data.");
        }
        catch (AsnContentException ex)
        {
            result.Add(FailureCode.ContentTypeAttributeInvalid, $"Signed content-type attribute malformed: {ex.Message}");
        }
    }

    private static void EvaluateMessageDigestAttribute(SignerInfo signer, VerificationResult result)
    {
        if (!TryGetSignedAttribute(signer, OidMessageDigestAttr, out _))
            result.Add(FailureCode.MessageDigestMismatch, "Missing signed message-digest attribute.");
    }

    private static DateTime? TryReadSigningTimeAttribute(SignerInfo signer, VerificationResult result)
    {
        if (!TryGetSignedAttribute(signer, OidSigningTime, out AsnEncodedData? stData) || stData is null)
        {
            // signingTime is optional under RFC 5652; absence is not a failure.
            return null;
        }
        try
        {
            return new Pkcs9SigningTime(stData.RawData).SigningTime.ToUniversalTime();
        }
        catch (CryptographicException ex)
        {
            result.Add(FailureCode.SigningTimeMissing, $"Signing-time attribute malformed: {ex.Message}");
            return null;
        }
    }

    private static (DateTimeOffset? Timestamp, SignedCms? TsaCms, X509Certificate2? TsaCert) EvaluateTimestamp(
        SignerInfo primarySigner,
        DateTime? claimedSigningTimeUtc,
        VerificationResult result)
    {
        if (!TryGetUnsignedAttribute(primarySigner, OidTimestampToken, out AsnEncodedData? tsData) || tsData is null)
        {
            result.Add(FailureCode.TimestampMissing, "Missing RFC 3161 timestamp token (signatureTimeStampToken unsigned attribute).");
            return (null, null, null);
        }

        if (!Rfc3161TimestampToken.TryDecode(tsData.RawData, out Rfc3161TimestampToken? token, out _) || token is null)
        {
            result.Add(FailureCode.TimestampMalformed, "Could not decode RFC 3161 timestamp token.");
            return (null, null, null);
        }

        X509Certificate2? tsaCert = null;
        if (!token.VerifySignatureForSignerInfo(primarySigner, out tsaCert) || tsaCert is null)
        {
            result.Add(FailureCode.TimestampBindingInvalid, "Timestamp does not cover the primary signature (VerifySignatureForSignerInfo failed).");
            tsaCert = null;
        }

        SignedCms tsaCms = token.AsSignedCms();
        DateTimeOffset tsaTime = token.TokenInfo.Timestamp;

        if (claimedSigningTimeUtc is { } claimed)
        {
            TimeSpan drift = (tsaTime - new DateTimeOffset(claimed, TimeSpan.Zero)).Duration();
            if (drift > SigningTimeTolerance)
                result.Add(FailureCode.SigningTimeMismatch,
                    $"Claimed signing-time {claimed:O} drifts {drift} from TSA timestamp {tsaTime:O} (max {SigningTimeTolerance}).");
        }

        return (tsaTime, tsaCms, tsaCert);
    }

    private static void EvaluateChain(
        X509Certificate2 leaf,
        X509Certificate2Collection extraStore,
        X509Certificate2Collection customRoots,
        DateTime verificationTime,
        RevocationCheckMode revocationMode,
        FailureCode timeoutCode,
        FailureCode revokedCode,
        FailureCode genericCode,
        string role,
        VerificationResult result)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = revocationMode switch
        {
            RevocationCheckMode.Online => X509RevocationMode.Online,
            RevocationCheckMode.Offline => X509RevocationMode.Offline,
            RevocationCheckMode.NoCheck => X509RevocationMode.NoCheck,
            _ => X509RevocationMode.Online,
        };
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
        chain.ChainPolicy.UrlRetrievalTimeout = RevocationRetrievalTimeout;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.VerificationTime = verificationTime;

        chain.ChainPolicy.ExtraStore.AddRange(extraStore);

        chain.ChainPolicy.CustomTrustStore.AddRange(customRoots);
        chain.ChainPolicy.CustomTrustStore.AddRange(s_osRoots.Value);

        bool ok = chain.Build(leaf);
        if (ok && chain.ChainStatus.Length == 0)
            return;

        X509ChainStatusFlags flags = X509ChainStatusFlags.NoError;
        foreach (X509ChainStatus s in chain.ChainStatus)
            flags |= s.Status;

        if ((flags & X509ChainStatusFlags.Revoked) != 0)
        {
            result.Add(revokedCode, $"{role} chain reports revoked certificate: {DescribeChainStatus(chain)}");
            return;
        }

        const X509ChainStatusFlags offline =
            X509ChainStatusFlags.OfflineRevocation |
            X509ChainStatusFlags.RevocationStatusUnknown;

        if ((flags & offline) != 0)
        {
            result.Add(timeoutCode,
                $"{role} chain could not confirm revocation status (offline or unreachable). Detail: {DescribeChainStatus(chain)}");
            return;
        }

        result.Add(genericCode, $"{role} chain build failed: {DescribeChainStatus(chain)}");
    }

    private static X509Certificate2Collection LoadOsRoots()
    {
        var store = new X509Certificate2Collection();
        foreach (StoreLocation loc in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            try
            {
                using var osStore = new X509Store(StoreName.Root, loc);
                osStore.Open(OpenFlags.ReadOnly);
                store.AddRange(osStore.Certificates);
            }
            catch (CryptographicException)
            {
                // Store may be unavailable on some platforms (e.g. LocalMachine on Linux). Skip.
            }
        }
        return store;
    }

    private static string DescribeChainStatus(X509Chain chain)
    {
        if (chain.ChainStatus.Length == 0) return "no status";
        var parts = new List<string>(chain.ChainStatus.Length);
        foreach (X509ChainStatus s in chain.ChainStatus)
            parts.Add($"{s.Status}:{s.StatusInformation?.Trim()}");
        return string.Join(" | ", parts);
    }

    private static void EvaluateJsonExpiration(byte[] content, DateTimeOffset signingTimeUtc, DateTimeOffset nowUtc, VerificationResult result)
    {
        JsonDocument doc;
        try
        {
            var jsonOptions = new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow };
            doc = JsonDocument.Parse(content, jsonOptions);
        }
        catch (JsonException ex)
        {
            result.Add(FailureCode.JsonParseFailed, $"Signed JSON content is not valid JSON: {ex.Message}");
            return;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                result.Add(FailureCode.ExpirationMissing, "Signed JSON content root is not an object.");
                return;
            }

            // Support both top-level "expiration" (legacy) and "signature.expiration" (v2 manifest format).
            if (!doc.RootElement.TryGetProperty("expiration", out JsonElement exp))
            {
                if (doc.RootElement.TryGetProperty("signature", out JsonElement sig) &&
                    sig.ValueKind == JsonValueKind.Object &&
                    sig.TryGetProperty("expiration", out exp))
                {
                    // found under "signature.expiration"
                }
                else
                {
                    result.Add(FailureCode.ExpirationMissing, "Signed JSON content has no 'expiration' property (checked top-level and 'signature.expiration').");
                    return;
                }
            }

            if (exp.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(exp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset expiration))
            {
                result.Add(FailureCode.ExpirationMalformed, "'expiration' must be an ISO-8601 date/time string.");
                return;
            }

            if (signingTimeUtc >= expiration)
                result.Add(FailureCode.SignedAfterExpiration,
                    $"Content was signed at {signingTimeUtc:O} which is not before its expiration {expiration:O}.");

            if (nowUtc >= expiration)
                result.Add(FailureCode.ExpiredNow,
                    $"Current time {nowUtc:O} is not before content expiration {expiration:O}.");
        }
    }
}
