// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// CMS detached-signature verifier for .NET release artifacts (manifest JSON or archives).
/// See <c>documentation/general/dotnetup/signature-verification.md</c> for the descriptive
/// behavior reference. The verifier is content-agnostic — the JSON expiration check (§9)
/// is opt-in via <see cref="SignatureVerificationOptions.RequireJsonExpirationField"/>.
/// </summary>
internal static class SignatureVerifier
{
    // Required signer subject DN (RDN set; OID-based, order-insensitive). Spec §5.1.
    private static readonly (string Oid, string Value)[] s_requiredSubjectRdns =
    [
        ("2.5.4.3",  "Microsoft Corporation"), // CN
        ("2.5.4.11", ".NET Release"),          // OU
        ("2.5.4.10", "Microsoft Corporation"), // O
        ("2.5.4.7",  "Redmond"),               // L
        ("2.5.4.8",  "Washington"),            // S
        ("2.5.4.6",  "US"),                    // C
    ];

    // Required signer issuer DN (DigiCert code-signing intermediate). Spec §5.2.
    private static readonly (string Oid, string Value)[] s_requiredIssuerRdns =
    [
        ("2.5.4.3",  "DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1"),
        ("2.5.4.10", "DigiCert, Inc."),
        ("2.5.4.6",  "US"),
    ];

    private const string EkuCodeSigning = "1.3.6.1.5.5.7.3.3";   // id-kp-codeSigning (RFC 5280 §4.2.1.12) https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.12
    private const string EkuTimeStamping = "1.3.6.1.5.5.7.3.8";  // id-kp-timeStamping (RFC 5280 §4.2.1.12) https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.12
    private const string EkuAnyExtended = "2.5.29.37.0";         // anyExtendedKeyUsage (RFC 5280 §4.2.1.12) https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.12

    private const string OidSigningTime = "1.2.840.113549.1.9.5";          // id-signingTime (RFC 5652 §11.3) https://datatracker.ietf.org/doc/html/rfc5652#section-11.3
    private const string OidContentTypeAttr = "1.2.840.113549.1.9.3";      // id-contentType (RFC 5652 §11.1) https://datatracker.ietf.org/doc/html/rfc5652#section-11.1
    private const string OidMessageDigestAttr = "1.2.840.113549.1.9.4";    // id-messageDigest (RFC 5652 §11.2) https://datatracker.ietf.org/doc/html/rfc5652#section-11.2
    private const string OidIdData = "1.2.840.113549.1.7.1";               // id-data (RFC 5652 §4) https://datatracker.ietf.org/doc/html/rfc5652#section-4
    private const string OidTimestampToken = "1.2.840.113549.1.9.16.2.14"; // id-aa-signatureTimeStampToken (RFC 3161 Appendix A https://datatracker.ietf.org/doc/html/rfc3161#appendix-A / RFC 5126 §6.1.1 https://datatracker.ietf.org/doc/html/rfc5126#section-6.1.1)

    private const string OidRsa = "1.2.840.113549.1.1.1"; // rsaEncryption (RFC 8017 Appendix C) https://datatracker.ietf.org/doc/html/rfc8017#appendix-C
    private const string OidEcdsa = "1.2.840.10045.2.1";  // id-ecPublicKey (RFC 5480 §2.1.1) https://datatracker.ietf.org/doc/html/rfc5480#section-2.1.1

    // SHA-2 digest algorithm OIDs (NIST CSOR 2.16.840.1.101.3.4.2; registered in RFC 5754 §2)
    // https://datatracker.ietf.org/doc/html/rfc5754#section-2
    // Default HashSet<string> equality comparer is ordinal, matching the OID strings.
    private static readonly HashSet<string> s_allowedDigestOids =
    [
        "2.16.840.1.101.3.4.2.1", // id-sha256
        "2.16.840.1.101.3.4.2.2", // id-sha384
        "2.16.840.1.101.3.4.2.3", // id-sha512
    ];

    private static readonly TimeSpan s_signingTimeTolerance = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_revocationRetrievalTimeout = TimeSpan.FromSeconds(30);

    // The OS root store (StoreName.Root for both CurrentUser and LocalMachine) is read into
    // every X509Chain we build. Opening the OS store on each call costs 50–200ms on Windows;
    // we build two chains per Verify (primary + TSA) so that's 4 store opens per call.
    // Cache the union once per process — chain build is read-only.
    private static readonly Lazy<X509Certificate2Collection> s_osRoots = new(LoadOsRoots, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Verifies <paramref name="content"/> against the detached CMS signature in <paramref name="signature"/>.
    /// In the default <see cref="VerificationMode.ShortCircuit"/> mode, returns on the first failure;
    /// in <see cref="VerificationMode.CollectAll"/> mode, runs every check and returns every failure.
    /// </summary>
    /// <param name="content">Bytes of the signed content (manifest JSON or archive).</param>
    /// <param name="signature">Bytes of the detached CMS / PKCS#7 SignedData blob (.p7s).</param>
    /// <param name="options">Trust anchors and policy knobs.</param>
    /// <param name="nowOverride">When set, used in place of <see cref="DateTimeOffset.UtcNow"/> for tests.</param>
    /// <param name="mode">Short-circuit (default) or collect-all. See <see cref="VerificationMode"/>.</param>
    public static VerificationResult Verify(
        byte[] content,
        byte[] signature,
        SignatureVerificationOptions options,
        DateTimeOffset? nowOverride = null,
        VerificationMode mode = VerificationMode.ShortCircuit)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(options);

        var result = new VerificationResult(shortCircuit: mode == VerificationMode.ShortCircuit);

        EvaluateTrustedRoots(options, result);
        if (result.ShouldStop) { return result; }

        DecodeCms(content, signature, result, out SignedCms? cms, out SignerInfo? signer, out X509Certificate2? signerCert);
        if (result.ShouldStop) { return result; }

        EvaluateAlgorithmPolicy(signer, signerCert, result);
        if (result.ShouldStop) { return result; }

        EvaluateSignerCertificatePolicy(signerCert, result);
        if (result.ShouldStop) { return result; }

        DateTime? claimedSigningTimeUtc = TryEvaluateSignedAttributes(signer, result);
        if (result.ShouldStop) { return result; }

        (DateTimeOffset? tsaTimestampUtc, SignedCms? tsaCms, X509Certificate2? tsaCert) =
            TryEvaluateTimestamp(signer, claimedSigningTimeUtc, result);
        if (result.ShouldStop) { return result; }

        EvaluateTimestampChain(tsaCert, tsaCms, tsaTimestampUtc, signer, options, result);
        if (result.ShouldStop) { return result; }

        EvaluatePrimaryChain(signerCert, cms, tsaTimestampUtc, options, nowOverride, result);
        if (result.ShouldStop) { return result; }

        MaybeEvaluateJsonExpiration(content, tsaTimestampUtc, options, nowOverride, result);

        return result;
    }

    /// <summary>
    /// Records <see cref="FailureCode.TrustedRootsEmpty"/> if either anchor collection is empty.
    /// Misconfigured callers (e.g. a bad PEM resource) would otherwise silently fall back to an
    /// OS-only chain build and accept things they shouldn't.
    /// </summary>
    private static void EvaluateTrustedRoots(SignatureVerificationOptions options, VerificationResult result)
    {
        if (options.TrustedCodeSigningRoots.Count == 0)
        {
            result.Add(FailureCode.TrustedRootsEmpty, "TrustedCodeSigningRoots is empty.");
        }
        if (options.TrustedTimestampRoots.Count == 0)
        {
            result.Add(FailureCode.TrustedRootsEmpty, "TrustedTimestampRoots is empty.");
        }
    }

    /// <summary>
    /// Decodes the detached CMS blob, runs <see cref="SignedCms.CheckSignature(bool)"/> for
    /// cryptographic integrity, and surfaces the signer + cert (when present) to the caller.
    /// All three out-params can be null on failure; downstream sections handle that with
    /// <see cref="VerificationResult.AddSkip"/> rather than short-circuiting.
    /// </summary>
    private static void DecodeCms(
        byte[] content,
        byte[] signature,
        VerificationResult result,
        out SignedCms? cms,
        out SignerInfo? signer,
        out X509Certificate2? signerCert)
    {
        cms = new SignedCms(new ContentInfo(new Oid(OidIdData), content), detached: true);
        signer = null;
        signerCert = null;

        try
        {
            cms.Decode(signature);
        }
        catch (CryptographicException ex)
        {
            result.Add(FailureCode.SigDecodeFailed, $"Signature is not a valid PKCS#7/CMS SignedData blob: {ex.Message}");
            cms = null;
            return;
        }

        if (cms.ContentInfo.ContentType.Value != OidIdData)
        {
            result.Add(FailureCode.SigNotCms, $"Unexpected encapsulated content-type OID {cms.ContentInfo.ContentType.Value}; expected id-data ({OidIdData}).");
        }

        if (cms.SignerInfos.Count != 1)
        {
            result.Add(FailureCode.SigMultipleSigners, $"Expected exactly one signer; found {cms.SignerInfos.Count}.");
        }

        if (cms.SignerInfos.Count >= 1)
        {
            signer = cms.SignerInfos[0];
            signerCert = signer.Certificate;
            if (signerCert is null)
            {
                result.Add(FailureCode.SignerCertMissing, "SignerInfo does not carry the signer certificate.");
            }
        }

        try
        {
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException ex)
        {
            result.Add(FailureCode.SigCryptoInvalid, $"CMS cryptographic verification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Enforces the digest + public-key algorithm allow-list from spec §4.
    /// </summary>
    private static void EvaluateAlgorithmPolicy(SignerInfo? signer, X509Certificate2? signerCert, VerificationResult result)
    {
        if (signer is not null)
        {
            string digestOid = signer.DigestAlgorithm.Value ?? string.Empty;
            if (!s_allowedDigestOids.Contains(digestOid))
            {
                result.Add(FailureCode.WeakDigest, $"Digest algorithm OID {digestOid} is not permitted.");
            }
        }
        else
        {
            result.AddSkip("Digest algorithm policy not evaluated: no SignerInfo available.");
        }

        if (signerCert is not null)
        {
            string keyOid = signerCert.PublicKey.Oid.Value ?? string.Empty;
            if (keyOid is not OidRsa and not OidEcdsa)
            {
                result.Add(FailureCode.WeakSignatureAlgorithm, $"Signer public-key algorithm OID {keyOid} is not permitted (RSA or ECDSA required).");
            }
        }
        else
        {
            result.AddSkip("Public-key algorithm policy not evaluated: no signer certificate available.");
        }
    }

    /// <summary>
    /// Pins the signer's subject DN, immediate-issuer DN, and EKU per spec §5.
    /// </summary>
    private static void EvaluateSignerCertificatePolicy(X509Certificate2? signerCert, VerificationResult result)
    {
        if (signerCert is null)
        {
            result.AddSkip("Subject DN not evaluated: no signer certificate available.");
            result.AddSkip("Issuer DN not evaluated: no signer certificate available.");
            result.AddSkip("Primary EKU not evaluated: no signer certificate available.");
            return;
        }

        if (!DistinguishedNameMatches(signerCert.SubjectName, s_requiredSubjectRdns, "Subject", out string subjectDetail))
        {
            result.Add(FailureCode.SubjectMismatch, subjectDetail);
        }

        if (!DistinguishedNameMatches(signerCert.IssuerName, s_requiredIssuerRdns, "Issuer", out string issuerDetail))
        {
            result.Add(FailureCode.IssuerMismatch, issuerDetail);
        }

        EvaluateEku(signerCert, EkuCodeSigning, primary: true, result);
    }

    /// <summary>
    /// PKCS#9 signed-attribute checks (spec §8). Returns the claimed signing time when present,
    /// for later TSA-drift comparison.
    /// </summary>
    private static DateTime? TryEvaluateSignedAttributes(SignerInfo? signer, VerificationResult result)
    {
        if (signer is null || signer.SignedAttributes.Count == 0)
        {
            return null;
        }

        EvaluateContentTypeAttribute(signer, result);
        EvaluateMessageDigestAttribute(signer, result);
        return TryReadSigningTimeAttribute(signer, result);
    }

    private static (DateTimeOffset? Timestamp, SignedCms? TsaCms, X509Certificate2? TsaCert) TryEvaluateTimestamp(
        SignerInfo? signer,
        DateTime? claimedSigningTimeUtc,
        VerificationResult result)
    {
        if (signer is null)
        {
            result.AddSkip("Timestamp not evaluated: no SignerInfo available.");
            return (null, null, null);
        }

        return EvaluateTimestamp(signer, claimedSigningTimeUtc, result);
    }

    /// <summary>
    /// Builds the TSA chain when a timestamp was extracted; otherwise records a skip.
    /// </summary>
    private static void EvaluateTimestampChain(
        X509Certificate2? tsaCert,
        SignedCms? tsaCms,
        DateTimeOffset? tsaTimestampUtc,
        SignerInfo? signer,
        SignatureVerificationOptions options,
        VerificationResult result)
    {
        if (tsaCert is null || tsaCms is null || tsaTimestampUtc is null)
        {
            if (signer is not null)
            {
                result.AddSkip("Timestamp chain not evaluated: timestamp could not be extracted.");
            }
            return;
        }

        EvaluateEku(tsaCert, EkuTimeStamping, primary: false, result);
        EvaluateChain(
            tsaCert,
            extraStore: tsaCms.Certificates,
            customRoots: options.TrustedTimestampRoots,
            // The TSA chain is built without a fixed VerificationTime so the chain engine
            // uses "now" — the TSA cert validity is what attests to *when* the timestamp
            // was issued, so anchoring its own validity check to its own output is
            // chicken-and-egg. Mirrors NuGet's CertificateChainUtility.SetCertBuildChainPolicy
            // which skips VerificationTime when CertificateType == Timestamp.
            verificationTime: null,
            applicationPolicyEku: EkuTimeStamping,
            revocationMode: options.RevocationMode,
            timeoutCode: FailureCode.TimestampRevocationUnavailable,
            revokedCode: FailureCode.TimestampChainFailed,
            genericCode: FailureCode.TimestampChainFailed,
            role: "timestamp authority",
            result);
    }

    /// <summary>
    /// Builds the primary signer chain at the TSA-attested time when available; otherwise
    /// falls back to "now" and records a skip explaining the fallback.
    /// </summary>
    private static void EvaluatePrimaryChain(
        X509Certificate2? signerCert,
        SignedCms? cms,
        DateTimeOffset? tsaTimestampUtc,
        SignatureVerificationOptions options,
        DateTimeOffset? nowOverride,
        VerificationResult result)
    {
        if (signerCert is null || cms is null)
        {
            result.AddSkip("Primary chain not evaluated: signer certificate or CMS missing.");
            return;
        }

        DateTime verificationTime = tsaTimestampUtc?.UtcDateTime ?? (nowOverride ?? DateTimeOffset.UtcNow).UtcDateTime;
        if (tsaTimestampUtc is null)
        {
            result.AddSkip($"Primary chain verification time fell back to current UTC ({verificationTime:O}) because no TSA timestamp was available.");
        }

        EvaluateChain(
            signerCert,
            extraStore: cms.Certificates,
            customRoots: options.TrustedCodeSigningRoots,
            verificationTime: verificationTime,
            applicationPolicyEku: EkuCodeSigning,
            revocationMode: options.RevocationMode,
            timeoutCode: FailureCode.RevocationUnavailable,
            revokedCode: FailureCode.Revoked,
            genericCode: FailureCode.ChainBuildFailed,
            role: "primary signer",
            result);
    }

    /// <summary>
    /// Spec §9 — JSON expiration policy. Skipped silently when the caller opted out via
    /// <see cref="SignatureVerificationOptions.RequireJsonExpirationField"/>; logged as a
    /// skip when no TSA timestamp anchors the comparison.
    /// </summary>
    private static void MaybeEvaluateJsonExpiration(
        byte[] content,
        DateTimeOffset? tsaTimestampUtc,
        SignatureVerificationOptions options,
        DateTimeOffset? nowOverride,
        VerificationResult result)
    {
        if (!options.RequireJsonExpirationField)
        {
            return;
        }

        if (tsaTimestampUtc is null)
        {
            result.AddSkip("JSON expiration policy not evaluated: no TSA timestamp.");
            return;
        }

        EvaluateJsonExpiration(content, tsaTimestampUtc.Value, nowOverride ?? DateTimeOffset.UtcNow, result);
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
            if (o.Value is null)
            {
                continue;
            }
            oids.Add(o.Value);
        }

        if (oids.Count != 1 || oids[0] != requiredOid || oids.Contains(EkuAnyExtended))
        {
            result.Add(
                primary ? FailureCode.EkuNotExclusiveCodeSign : FailureCode.TimestampEkuInvalid,
                $"Certificate EKU must contain exactly {requiredOid} and nothing else. Found: [{string.Join(", ", oids)}].");
        }
    }

    private static bool TryGetSignedAttribute(SignerInfo signer, string oid, [NotNullWhen(true)] out AsnEncodedData? data)
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

    private static bool TryGetUnsignedAttribute(SignerInfo signer, string oid, [NotNullWhen(true)] out AsnEncodedData? data)
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
        if (!TryGetSignedAttribute(signer, OidContentTypeAttr, out AsnEncodedData? ctData))
        {
            result.Add(FailureCode.ContentTypeAttributeInvalid, "Missing signed content-type attribute.");
            return;
        }
        try
        {
            string ctOid = AsnDecoder.ReadObjectIdentifier(ctData.RawData, AsnEncodingRules.DER, out _);
            if (ctOid != OidIdData)
            {
                result.Add(FailureCode.ContentTypeAttributeInvalid, $"Signed content-type is {ctOid}; expected id-data.");
            }
        }
        catch (AsnContentException ex)
        {
            result.Add(FailureCode.ContentTypeAttributeInvalid, $"Signed content-type attribute malformed: {ex.Message}");
        }
    }

    private static void EvaluateMessageDigestAttribute(SignerInfo signer, VerificationResult result)
    {
        if (!TryGetSignedAttribute(signer, OidMessageDigestAttr, out _))
        {
            result.Add(FailureCode.MessageDigestMismatch, "Missing signed message-digest attribute.");
        }
    }

    private static DateTime? TryReadSigningTimeAttribute(SignerInfo signer, VerificationResult result)
    {
        if (!TryGetSignedAttribute(signer, OidSigningTime, out AsnEncodedData? stData))
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
        if (!TryGetUnsignedAttribute(primarySigner, OidTimestampToken, out AsnEncodedData? tsData))
        {
            result.Add(FailureCode.TimestampMissing, "Missing RFC 3161 timestamp token (signatureTimeStampToken unsigned attribute).");
            return (null, null, null);
        }

        if (!Rfc3161TimestampToken.TryDecode(tsData.RawData, out Rfc3161TimestampToken? token, out _) || token is null)
        {
            result.Add(FailureCode.TimestampMalformed, "Could not decode RFC 3161 timestamp token.");
            return (null, null, null);
        }

        if (!token.VerifySignatureForSignerInfo(primarySigner, out X509Certificate2? tsaCert) || tsaCert is null)
        {
            result.Add(FailureCode.TimestampBindingInvalid, "Timestamp does not cover the primary signature (VerifySignatureForSignerInfo failed).");
            tsaCert = null;
        }

        SignedCms tsaCms = token.AsSignedCms();
        DateTimeOffset tsaTime = token.TokenInfo.Timestamp;

        if (claimedSigningTimeUtc is { } claimed)
        {
            TimeSpan drift = (tsaTime - new DateTimeOffset(claimed, TimeSpan.Zero)).Duration();
            if (drift > s_signingTimeTolerance)
            {
                result.Add(FailureCode.SigningTimeMismatch,
                    $"Claimed signing-time {claimed:O} drifts {drift} from TSA timestamp {tsaTime:O} (max {s_signingTimeTolerance}).");
            }
        }

        return (tsaTime, tsaCms, tsaCert);
    }

    private static void EvaluateChain(
        X509Certificate2 leaf,
        X509Certificate2Collection extraStore,
        X509Certificate2Collection customRoots,
        DateTime? verificationTime,
        string applicationPolicyEku,
        RevocationCheckMode revocationMode,
        FailureCode timeoutCode,
        FailureCode revokedCode,
        FailureCode genericCode,
        string role,
        VerificationResult result)
    {
        using var chain = new X509Chain();
        ConfigureChainPolicy(chain, extraStore, customRoots, verificationTime, applicationPolicyEku, revocationMode);

        bool ok = BuildWithUntrustedRootRetry(chain, leaf);
        try
        {
            InterpretChainStatus(chain, ok, role, timeoutCode, revokedCode, genericCode, result);
        }
        finally
        {
            // X509Certificate2 holds an OS handle. Disposing each chain element's certificate
            // immediately avoids finalizer pressure (and on Linux can prevent ulimit issues
            // under heavy verification load). Mirrors NuGet's X509ChainHolder.Dispose.
            foreach (X509ChainElement element in chain.ChainElements)
            {
                element.Certificate.Dispose();
            }
        }
    }

    /// <summary>
    /// Sets up <see cref="X509ChainPolicy"/> per the spec: custom-root trust (no Windows
    /// system roots beyond what we add explicitly), entire-chain revocation, EKU enforcement
    /// at chain-build time (defense-in-depth on top of the leaf-only EKU bag check), and a
    /// strict <see cref="X509VerificationFlags.NoFlag"/> — release manifests are FRESH
    /// artifacts so we surface NotTimeValid as a failure rather than ignoring it.
    /// </summary>
    private static void ConfigureChainPolicy(
        X509Chain chain,
        X509Certificate2Collection extraStore,
        X509Certificate2Collection customRoots,
        DateTime? verificationTime,
        string applicationPolicyEku,
        RevocationCheckMode revocationMode)
    {
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = revocationMode switch
        {
            RevocationCheckMode.Online => X509RevocationMode.Online,
            RevocationCheckMode.Offline => X509RevocationMode.Offline,
            RevocationCheckMode.NoCheck => X509RevocationMode.NoCheck,
            _ => X509RevocationMode.Online,
        };
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
        // Defense-in-depth: tell the chain engine to enforce the EKU as part of the build.
        // Mirrors NuGet's CertificateChainUtility.SetCertBuildChainPolicy. Catches
        // mis-purposed intermediates / cross-signed scenarios where an intermediate
        // constrains the leaf's permitted purposes.
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(applicationPolicyEku));
        chain.ChainPolicy.UrlRetrievalTimeout = s_revocationRetrievalTimeout;
        // We deliberately do NOT set X509VerificationFlags.IgnoreNotTimeValid here,
        // unlike NuGet's package-verification path. Release manifests are intended to be
        // FRESH artifacts: an expired signer cert at time of consumption means the
        // manifest is stale even if the signature was correct when produced. NuGet
        // ignores NotTimeValid because packages are immutable historical artifacts;
        // we are not. Surface NotTimeValid as ChainBuildFailed.
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        if (verificationTime.HasValue)
        {
            chain.ChainPolicy.VerificationTime = verificationTime.Value;
        }
        // else: TSA chain — use "now" so we don't anchor TSA validity to its own output.

        chain.ChainPolicy.ExtraStore.AddRange(extraStore);
        chain.ChainPolicy.CustomTrustStore.AddRange(customRoots);
        chain.ChainPolicy.CustomTrustStore.AddRange(s_osRoots.Value);
    }

    /// <summary>
    /// Maps post-build <see cref="X509Chain.ChainStatus"/> flags to the role-specific
    /// <see cref="FailureCode"/>s. Revoked &gt; offline-revocation &gt; generic build failure.
    /// </summary>
    private static void InterpretChainStatus(
        X509Chain chain,
        bool buildSucceeded,
        string role,
        FailureCode timeoutCode,
        FailureCode revokedCode,
        FailureCode genericCode,
        VerificationResult result)
    {
        if (buildSucceeded && chain.ChainStatus.Length == 0)
        {
            return;
        }

        X509ChainStatusFlags flags = X509ChainStatusFlags.NoError;
        foreach (X509ChainStatus s in chain.ChainStatus)
        {
            flags |= s.Status;
        }

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

    /// <summary>
    /// Builds the chain, retrying on transient <see cref="X509ChainStatusFlags.UntrustedRoot" />
    /// failures on Windows. Mirrors NuGet's RetriableX509ChainBuildPolicy, which defends
    /// against a documented Windows transient where the OS root store reports UntrustedRoot
    /// briefly under load. Windows-only because NuGet's factory only enables retry there.
    /// </summary>
    private static bool BuildWithUntrustedRootRetry(X509Chain chain, X509Certificate2 leaf)
    {
        const int RetryCount = 3;
        TimeSpan sleepInterval = TimeSpan.FromMilliseconds(1000);

        bool ok = chain.Build(leaf);
        if (ok || !OperatingSystem.IsWindows())
        {
            return ok;
        }

        for (int i = 0; i < RetryCount && !ok; i++)
        {
            bool hasUntrustedRoot = false;
            foreach (X509ChainStatus status in chain.ChainStatus)
            {
                if ((status.Status & X509ChainStatusFlags.UntrustedRoot) != 0)
                {
                    hasUntrustedRoot = true;
                    break;
                }
            }
            if (!hasUntrustedRoot)
            {
                break;
            }

            Thread.Sleep(sleepInterval);
            ok = chain.Build(leaf);
        }

        return ok;
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
        if (chain.ChainStatus.Length == 0)
        {
            return "no status";
        }
        var parts = new List<string>(chain.ChainStatus.Length);
        foreach (X509ChainStatus s in chain.ChainStatus)
        {
            parts.Add($"{s.Status}:{s.StatusInformation?.Trim()}");
        }
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
            {
                result.Add(FailureCode.SignedAfterExpiration,
                    $"Content was signed at {signingTimeUtc:O} which is not before its expiration {expiration:O}.");
            }

            if (nowUtc >= expiration)
            {
                result.Add(FailureCode.ExpiredNow,
                    $"Current time {nowUtc:O} is not before content expiration {expiration:O}.");
            }
        }
    }
}
