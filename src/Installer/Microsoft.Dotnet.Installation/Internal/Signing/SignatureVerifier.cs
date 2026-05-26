// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// CMS detached-signature verifier for .NET release artifacts (manifest JSON or archives).
/// See <c>documentation/general/dotnetup/signature-verification.md</c> for the descriptive
/// behavior reference. Content-agnostic; the JSON expiration check (§9) is opt-in via
/// <see cref="SignatureVerificationOptions.RequireJsonExpirationField"/>.
/// </summary>
/// <remarks>
/// Constants/tables are split across partial files for navigability:
/// <list type="bullet">
///   <item><c>SignatureVerifier.DistinguishedNames.cs</c> — pinned signer / TSA-issuer DNs.</item>
///   <item><c>SignatureVerifier.Ekus.cs</c> — EKU OIDs and the code-signing permitted set.</item>
///   <item><c>SignatureVerifier.PqcOids.cs</c> — ML-DSA / SLH-DSA / Composite ML-DSA OID tables.</item>
///   <item><c>SignatureVerifier.DigestOids.cs</c> — SHA-2 / SHA-3 / SHAKE digest OID allow-list.</item>
///   <item><c>SignatureVerifier.UnsupportedOids.cs</c> — explicitly-rejected key + digest OIDs.</item>
/// </list>
/// </remarks>
internal static partial class SignatureVerifier
{
    private const string OidIdData = "1.2.840.113549.1.7.1";               // id-data (RFC 5652 §4) https://datatracker.ietf.org/doc/html/rfc5652#section-4
    private const string OidTimestampToken = "1.2.840.113549.1.9.16.2.14"; // id-aa-signatureTimeStampToken (RFC 3161 Appendix A https://datatracker.ietf.org/doc/html/rfc3161#appendix-A; SET OF TimeStampToken per RFC 3161 §2.4.2 https://datatracker.ietf.org/doc/html/rfc3161#section-2.4.2)

    private const string OidRsa = "1.2.840.113549.1.1.1"; // rsaEncryption (RFC 8017 Appendix C) https://datatracker.ietf.org/doc/html/rfc8017#appendix-C

    private const int MinRsaKeySizeBits = 4096;

    private static readonly HashSet<string> s_allowedPublicKeyOids;
    private static readonly HashSet<string> s_allowedDigestOids;

#pragma warning disable CA1810 // CA1810: explicit static ctor needed for cross-partial init order. IDE0079: IDE incorrectly reports the CA1810 suppression as unnecessary.
    static SignatureVerifier()
#pragma warning restore CA1810
    {
        s_allowedPublicKeyOids = new HashSet<string>(s_pqcPureKeyOids) { OidRsa };
        s_allowedDigestOids = new HashSet<string>(s_pqcSignatureOids)
        {
            OidIdSha256,
            OidIdSha384,
            OidIdSha512,
        };
    }

    private const int RevocationRetrievalTimeoutSeconds = 30;
    private static readonly TimeSpan s_revocationRetrievalTimeout = TimeSpan.FromSeconds(RevocationRetrievalTimeoutSeconds);

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
        var result = new VerificationResult(shortCircuit: mode == VerificationMode.ShortCircuit);

        SignedCms? cms = null;
        SignerInfo? signer = null;
        X509Certificate2? signerCert = null;
        DateTimeOffset? tsaTimestampUtc = null;
        IReadOnlyList<TsaToken> tsaTokens = Array.Empty<TsaToken>();

        var steps = new Action[]
        {
            () => EvaluateTrustedRootOptions(options, result),
            () => DecodeCms(content, signature, result, out cms, out signer, out signerCert),
            () => EvaluateAlgorithmPolicy(signer, signerCert, result),
            () => EvaluateSignerCertificatePolicy(signerCert, result),
            () => EvaluateTimestamp(signer, result, out tsaTimestampUtc, out tsaTokens),
            () => EvaluateTimestampChains(tsaTokens, signer, options, nowOverride, result),
            () => EvaluatePrimaryChain(signerCert, cms, tsaTimestampUtc, options, nowOverride, result),
            () => MaybeEvaluateJsonExpiration(content, tsaTimestampUtc, options, nowOverride, result),
        };

        try
        {
            foreach (var step in steps)
            {
                if (result.ShouldStop) { break; }
                step();
            }
        }
        finally
        {
            // Dispose the X509Certificate2 handles we surfaced from the CMS structures.
            // Releasing them eagerly avoids finalizer-queue pressure on long-lived hosts.
            signerCert?.Dispose();
            foreach (TsaToken token in tsaTokens)
            {
                token.Cert.Dispose();
            }
        }

        return result;
    }

    /// <summary>
    /// Records <see cref="FailureCode.TrustedRootsEmpty"/> if either anchor collection is empty.
    /// Misconfigured callers (e.g. a bad PEM resource) would otherwise silently fall back to an
    /// OS-only chain build and accept things they shouldn't. This evaluates only the option
    /// configuration; it does not inspect the signature itself.
    /// </summary>
    private static void EvaluateTrustedRootOptions(SignatureVerificationOptions options, VerificationResult result)
    {
        if (options.TrustedCodeSigningRoots.Count == 0)
        {
            result.Add(FailureCode.TrustedRootsEmpty, $"{nameof(SignatureVerificationOptions.TrustedCodeSigningRoots)} is empty.");
        }
        if (options.TrustedTimestampRoots.Count == 0)
        {
            result.Add(FailureCode.TrustedRootsEmpty, $"{nameof(SignatureVerificationOptions.TrustedTimestampRoots)} is empty.");
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

        if (!EvaluateEncapContentType(cms, result))
        {
            return;
        }

        // SignedCms.SignerInfos allocates a fresh collection on every read — cache it once.
        // SignedCms.Decode + CheckSignature already enforce the CMS SignedData shape,
        // so this and the eContentType check above are the only structural assertions
        // the verifier layers on top.
        SignerInfoCollection signerInfos = cms.SignerInfos;
        if (signerInfos.Count != 1)
        {
            result.Add(FailureCode.SigMultipleSigners, $"Expected exactly one signer; found {signerInfos.Count}.");
            if (signerInfos.Count == 0)
            {
                return;
            }
        }

        signer = signerInfos[0];
        signerCert = signer.Certificate;
        if (signerCert is null)
        {
            result.Add(FailureCode.SignerCertMissing, "SignerInfo does not carry the signer certificate.");
        }

        try
        {
            // verifySignatureOnly:true checks message-digest + signature-value only.
            // We deliberately do NOT let CheckSignature build the chain here: passing false
            // would build it against the OS root store with default ChainPolicy.
            // EvaluateTimestampChain builds both chains explicitly with the spec policy.
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException ex)
        {
            result.Add(FailureCode.SigCryptoInvalid, $"CMS cryptographic verification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Spec §2: <c>encapContentInfo.eContentType</c> MUST be <c>id-data</c>.
    /// <see cref="SignedCms.CheckSignature(bool)"/> does NOT cross-validate the decoded
    /// eContentType against the <see cref="ContentInfo"/> we supplied for the detached
    /// decode — it just uses what we passed in. So a producer that ships a CMS with
    /// <c>eContentType=1.2.3.4</c> would slip past unless we check explicitly here.
    /// Returns <see langword="true"/> when the eContentType matches; otherwise records
    /// <see cref="FailureCode.SigDecodeFailed"/> and returns <see langword="false"/>.
    /// </summary>
    private static bool EvaluateEncapContentType(SignedCms cms, VerificationResult result)
    {
        string? actualContentType = cms.ContentInfo.ContentType.Value;
        if (string.Equals(actualContentType, OidIdData, StringComparison.Ordinal))
        {
            return true;
        }

        result.Add(FailureCode.SigDecodeFailed,
            $"CMS encapContentInfo.eContentType is '{actualContentType ?? "<null>"}'; expected id-data ({OidIdData}).");
        return false;
    }

    /// <summary>
    /// Enforces the digest + public-key algorithm allow-list from spec §4.
    /// </summary>
    /// <remarks>
    /// When <paramref name="signer"/> or <paramref name="signerCert"/> is null we record an
    /// <see cref="VerificationResult.AddSkip"/> rather than a fresh failure: the missing
    /// signer / signer-cert is already surfaced upstream by <see cref="DecodeCms"/> as one of
    /// <see cref="FailureCode.SigDecodeFailed"/>, <see cref="FailureCode.SigMultipleSigners"/>,
    /// or <see cref="FailureCode.SignerCertMissing"/>.
    /// The same pattern is used in <see cref="EvaluateSignerCertificatePolicy"/>, <see cref="EvaluateTimestamp"/>, and <see cref="EvaluatePrimaryChain"/>.
    /// </remarks>
    private static void EvaluateAlgorithmPolicy(SignerInfo? signer, X509Certificate2? signerCert, VerificationResult result)
    {
        if (signer is not null)
        {
            string digestOid = signer.DigestAlgorithm.Value ?? string.Empty;
            if (!s_allowedDigestOids.Contains(digestOid))
            {
                result.Add(FailureCode.WeakDigest, $"Digest algorithm OID '{digestOid}' is not permitted.");
            }
        }
        else
        {
            result.AddSkip("Digest algorithm policy not evaluated: no SignerInfo available.");
        }

        if (signerCert is not null)
        {
            string keyOid = signerCert.PublicKey.Oid.Value ?? string.Empty;
            if (!s_allowedPublicKeyOids.Contains(keyOid))
            {
                result.Add(FailureCode.SignatureAlgorithmNotPermitted,
                    $"Signer public-key algorithm OID '{keyOid}' is not permitted. Allowed: RSA (≥4096-bit), ML-DSA, SLH-DSA, Composite ML-DSA.");
            }
            else if (keyOid == OidRsa)
            {
                using RSA? rsa = signerCert.GetRSAPublicKey();
                if (rsa is null)
                {
                    result.Add(FailureCode.Unexpected,
                        "Signer certificate advertises RSA but the public key could not be loaded for inspection.");
                }
                else if (rsa.KeySize < MinRsaKeySizeBits)
                {
                    result.Add(FailureCode.WeakSignatureKey,
                        $"Signer RSA key size {rsa.KeySize} bits is below the required minimum of {MinRsaKeySizeBits} bits.");
                }
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
    /// Builds and validates the X.509 chain for every TSA cert collected by
    /// <see cref="EvaluateTimestamp"/>. signature-verification.md §7: every TSA chain
    /// must build at its own <c>genTime</c>, every TSA leaf must satisfy the EKU +
    /// issuer-DN pins, and the TST with the greatest <c>genTime</c> must additionally
    /// build at the current clock. Failing any single TSA invalidates the signature.
    /// </summary>

    private static void EvaluateTimestampChains(
        IReadOnlyList<TsaToken> tokens,
        SignerInfo? signer,
        SignatureVerificationOptions options,
        DateTimeOffset? nowOverride,
        VerificationResult result)
    {
        if (tokens.Count == 0)
        {
            if (signer is not null)
            {
                result.AddSkip("Timestamp chain(s) not evaluated: no valid TSA token was extracted (see upstream Timestamp* failure for the specific cause).");
            }
            return;
        }

        // Per RFC 3161 §2.4.2, `id-aa-signatureTimeStampToken` carries a SET OF TimeStampToken;
        // when more than one is present they are sibling attestations over the same primary
        // SignerInfo.signature value (not nested ATSv3 archive-time-stamps). The TST with the
        // greatest genTime is the signer's most recent attestation; that one is also re-validated
        // against the current clock inside the loop below as a freshness anchor (see comment
        // there for the threat model).
        DateTime nowUtc = (nowOverride ?? DateTimeOffset.UtcNow).UtcDateTime;
        TsaToken latest = tokens.MaxBy(static t => t.Time)!;

        foreach (TsaToken token in tokens)
        {
            EvaluateTimestampEku(token.Cert, result);

            if (!DistinguishedNameMatches(token.Cert.IssuerName, s_requiredTimestampIssuerRdns, "TSA Issuer", out string tsaIssuerDetail))
            {
                result.Add(FailureCode.TimestampIssuerMismatch, tsaIssuerDetail);
            }

            BuildTimestampChain(token, options, token.Time.UtcDateTime, result);

            if (ReferenceEquals(token, latest))
            {
                // Freshness anchor: the most-recent (greatest-genTime) TST's chain must also
                // build at the current clock. Without this an attacker could replay an old
                // captured signature whose TSA cert has since been revoked or expired (the
                // historical-genTime build can't see CRL/OCSP entries dated after that genTime).
                BuildTimestampChain(token, options, nowUtc, result);
            }
        }
    }

    private static void BuildTimestampChain(
        TsaToken token,
        SignatureVerificationOptions options,
        DateTime verificationTime,
        VerificationResult result)
    {
        EvaluateChain(
            token.Cert,
            extraStore: token.Cms.Certificates,
            customRoots: options.TrustedTimestampRoots,
            verificationTime: verificationTime,
            applicationPolicyEku: EkuTimeStamping,
            revocationMode: options.RevocationMode,
            kind: ChainKind.TimestampAuthority,
            result);
    }

    /// <summary>
    /// Builds the primary signer chain at the TSA-attested time when available.
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
            kind: ChainKind.PrimarySigner,
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
                detail = $"{label} contains a multi-valued RDN which is not permitted. {label}='{dn.Name}'";
                return false;
            }

            if (type?.Value is null || value is null)
            {
                detail = $"{label} contains an unparseable RDN. {label}='{dn.Name}'";
                return false;
            }
            actual.Add((type.Value, value));
        }

        if (actual.Count != required.Length)
        {
            detail = $"{label} has {actual.Count} RDNs; expected exactly {required.Length}. {label}='{dn.Name}'";
            return false;
        }

        foreach (var req in required)
        {
            int idx = actual.FindIndex(a =>
                string.Equals(a.Oid, req.Oid, StringComparison.Ordinal) &&
                string.Equals(a.Value, req.Value, StringComparison.Ordinal));
            if (idx < 0)
            {
                detail = $"{label} missing required RDN {req.Oid}='{req.Value}'. {label}='{dn.Name}'";
                return false;
            }
            actual.RemoveAt(idx);
        }

        detail = string.Empty;
        return true;
    }

    /// <summary>
    /// Evaluates the Extended Key Usage extension on the primary (code-signing) signer
    /// certificate. Policy follows CA/Browser Forum Code Signing BR §7.1.2.3(f):
    /// <list type="bullet">
    /// <item>Exactly one EKU extension MUST be present (<see cref="FailureCode.EkuMultipleExtensions"/>).</item>
    /// <item><c>id-kp-codeSigning</c> MUST be present (<see cref="FailureCode.EkuMissing"/>).</item>
    /// <item><c>anyExtendedKeyUsage</c> and <c>id-kp-serverAuth</c> MUST NOT be present
    /// (<see cref="FailureCode.EkuNotExclusiveCodeSign"/>).</item>
    /// <item>Any OID outside the CAB-Forum-permitted set { codeSigning, Lifetime Signing,
    /// emailProtection, Document Signing } is rejected (<see cref="FailureCode.EkuNotExclusiveCodeSign"/>).</item>
    /// </list>
    /// The TSA cert uses a stricter "id-kp-timeStamping only" policy per RFC 3161 §2.3;
    /// see <see cref="EvaluateTimestampEku"/>.
    /// </summary>
    private static void EvaluateEku(X509Certificate2 cert, string requiredOid, bool primary, VerificationResult result)
    {
        if (!TryGetSingleEkuExtension(cert, primary, result, out X509EnhancedKeyUsageExtension? eku))
        {
            return;
        }

        List<string> oids = ReadEkuOids(eku);

        if (!oids.Contains(requiredOid))
        {
            result.Add(FailureCode.EkuMissing,
                $"Certificate '{cert.Subject}' EKU does not contain required OID {requiredOid}. Found: [{string.Join(", ", oids)}].");
        }

        if (oids.Contains(EkuAnyExtended) || oids.Contains(EkuServerAuth))
        {
            result.Add(FailureCode.EkuNotExclusiveCodeSign,
                $"Certificate '{cert.Subject}' EKU contains a disallowed OID (anyExtendedKeyUsage or id-kp-serverAuth). Found: [{string.Join(", ", oids)}].");
            return;
        }

        // Per CAB-Forum BR §7.1.2.3(f), permitted extras for a code-signing leaf are limited
        // to the set above. Anything else — even an otherwise-innocuous PKIX OID — is
        // rejected so a misconfigured cert does not silently slip through.
        foreach (string oid in oids)
        {
            if (!s_codeSigningPermittedEkus.Contains(oid))
            {
                result.Add(FailureCode.EkuNotExclusiveCodeSign,
                    $"Certificate '{cert.Subject}' EKU contains disallowed OID '{oid}'. Permitted per CA/Browser Forum BR §7.1.2.3(f): [{string.Join(", ", s_codeSigningPermittedEkus)}].");
                return;
            }
        }
    }

    /// <summary>
    /// Evaluates the EKU on the TSA certificate. Per RFC 3161 §2.3 the TSA cert
    /// "MUST contain only one instance of the extended key usage field extension … with
    /// KeyPurposeID having value: id-kp-timeStamping."
    /// </summary>
    private static void EvaluateTimestampEku(X509Certificate2 cert, VerificationResult result)
    {
        if (!TryGetSingleEkuExtension(cert, primary: false, result, out X509EnhancedKeyUsageExtension? eku))
        {
            return;
        }

        List<string> oids = ReadEkuOids(eku);
        if (oids.Count != 1 || oids[0] != EkuTimeStamping)
        {
            result.Add(FailureCode.TimestampEkuInvalid,
                $"TSA certificate '{cert.Subject}' EKU must contain exactly {EkuTimeStamping} (id-kp-timeStamping) and nothing else (RFC 3161 §2.3). Found: [{string.Join(", ", oids)}].");
        }
    }

    /// <summary>
    /// Locates the single EKU extension on <paramref name="cert"/>, recording an appropriate
    /// failure if there are zero or two-plus EKU extensions.
    /// </summary>
    private static bool TryGetSingleEkuExtension(X509Certificate2 cert, bool primary, VerificationResult result, [NotNullWhen(true)] out X509EnhancedKeyUsageExtension? eku)
    {
        eku = null;
        X509EnhancedKeyUsageExtension? found = null;
        int count = 0;
        foreach (X509Extension ext in cert.Extensions)
        {
            if (ext is X509EnhancedKeyUsageExtension e)
            {
                found = e;
                count++;
            }
        }

        if (count == 0)
        {
            result.Add(
                primary ? FailureCode.EkuMissing : FailureCode.TimestampEkuInvalid,
                $"Certificate '{cert.Subject}' has no Extended Key Usage extension.");
            return false;
        }

        if (count > 1)
        {
            result.Add(
                primary ? FailureCode.EkuMultipleExtensions : FailureCode.TimestampEkuInvalid,
                $"Certificate '{cert.Subject}' has {count} Extended Key Usage extensions; RFC 5280 §4.2.1.12 requires exactly one (multiple KeyPurposeID values are encoded as a sequence within a single extension).");
            return false;
        }

        eku = found!; // count == 1 — `found` was assigned exactly once above.
        return true;
    }

    /// <summary>
    /// Materializes the EKU's KeyPurposeId OIDs into a plain <see cref="List{T}"/> of strings.
    /// Prevents reallocating a fresh <see cref="OidCollection"/> which is what .EnhancedKeyUsages does.
    /// </summary>
    private static List<string> ReadEkuOids(X509EnhancedKeyUsageExtension eku)
    {
        OidCollection usages = eku.EnhancedKeyUsages;
        var oids = new List<string>(usages.Count);
        foreach (Oid o in usages)
        {
            if (o.Value is not null)
            {
                oids.Add(o.Value);
            }
        }
        return oids;
    }

    /// <summary>
    /// A single RFC 3161 timestamp token that has been decoded and cryptographically
    /// bound to the primary signer. Carries the timestamp instant, the TSA leaf cert,
    /// and the TSA's own SignedCms (for chain-building extraStore).
    /// </summary>
    private sealed record TsaToken(SignedCms Cms, X509Certificate2 Cert, DateTimeOffset Time);

    private static void EvaluateTimestamp(
        SignerInfo? primarySigner,
        VerificationResult result,
        out DateTimeOffset? authoritativeTime,
        out IReadOnlyList<TsaToken> tokens)
    {
        authoritativeTime = null;
        tokens = Array.Empty<TsaToken>();

        if (primarySigner is null)
        {
            result.AddSkip("Timestamp not evaluated: no SignerInfo available.");
            return;
        }

        // Collect every id-aa-signatureTimeStampToken unsigned-attribute value. RFC 3161
        var rawTokens = CollectTimestampTokenBytes(primarySigner);
        if (rawTokens.Count == 0)
        {
            result.Add(FailureCode.TimestampMissing, "Missing RFC 3161 timestamp token (signatureTimeStampToken unsigned attribute).");
            return;
        }

        var decoded = new List<TsaToken>(rawTokens.Count);
        for (int i = 0; i < rawTokens.Count; i++)
        {
            string positionLabel = rawTokens.Count == 1 ? "" : $" #{i + 1} of {rawTokens.Count}";
            if (TryDecodeAndBindToken(rawTokens[i], primarySigner, positionLabel, result) is { } token)
            {
                decoded.Add(token);
            }
        }

        if (decoded.Count == 0)
        {
            // Every TST failed to decode or bind. Per-token failures were recorded above.
            return;
        }

        DateTimeOffset earliest = decoded[0].Time;
        for (int i = 1; i < decoded.Count; i++)
        {
            if (decoded[i].Time < earliest)
            {
                earliest = decoded[i].Time;
            }
        }

        authoritativeTime = earliest;
        tokens = decoded;
    }

    private static List<byte[]> CollectTimestampTokenBytes(SignerInfo primarySigner)
    {
        var rawTokens = new List<byte[]>();
        foreach (CryptographicAttributeObject attr in primarySigner.UnsignedAttributes)
        {
            if (attr.Oid.Value != OidTimestampToken)
            {
                continue;
            }
            foreach (AsnEncodedData v in attr.Values)
            {
                rawTokens.Add(v.RawData);
            }
        }
        return rawTokens;
    }

    private static TsaToken? TryDecodeAndBindToken(byte[] raw, SignerInfo primarySigner, string positionLabel, VerificationResult result)
    {
        if (!Rfc3161TimestampToken.TryDecode(raw, out Rfc3161TimestampToken? token, out _) || token is null)
        {
            result.Add(FailureCode.TimestampMalformed, $"Could not decode RFC 3161 timestamp token{positionLabel}.");
            return null;
        }

        if (!token.VerifySignatureForSignerInfo(primarySigner, out X509Certificate2? tsaCert) || tsaCert is null)
        {
            result.Add(FailureCode.TimestampBindingInvalid, $"RFC 3161 timestamp token{positionLabel} does not cover the primary signature (VerifySignatureForSignerInfo failed).");
            return null;
        }

        return new TsaToken(token.AsSignedCms(), tsaCert, token.TokenInfo.Timestamp);
    }

    /// <summary>
    /// Identifies which of the two chains is being built so <see cref="EvaluateChain"/> /
    /// <see cref="InterpretChainStatus"/> can pick the correct role label and the correct
    /// triple of <see cref="FailureCode"/>s (timeout / revoked / generic-build-failure).
    /// The mapping is fixed so callers cannot mismatch the codes with the role.
    /// </summary>
    private enum ChainKind
    {
        PrimarySigner,
        TimestampAuthority,
    }

    private static (string Role, FailureCode Timeout, FailureCode Revoked, FailureCode Generic) GetChainCodes(ChainKind kind) => kind switch
    {
        ChainKind.PrimarySigner => ("primary signer", FailureCode.RevocationUnavailable, FailureCode.Revoked, FailureCode.ChainBuildFailed),
        ChainKind.TimestampAuthority => ("timestamp authority", FailureCode.TimestampRevocationUnavailable, FailureCode.TimestampChainFailed, FailureCode.TimestampChainFailed),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static void EvaluateChain(
        X509Certificate2 leaf,
        X509Certificate2Collection extraStore,
        X509Certificate2Collection customRoots,
        DateTime? verificationTime,
        string applicationPolicyEku,
        RevocationCheckMode revocationMode,
        ChainKind kind,
        VerificationResult result)
    {
        using var chain = new X509Chain();
        ConfigureChainPolicy(chain, extraStore, customRoots, verificationTime, applicationPolicyEku, revocationMode);

        bool ok;
        try
        {
            ok = chain.Build(leaf);
        }
        catch (CryptographicException ex)
        {
            var (role, _, _, genericCode) = GetChainCodes(kind);
            result.Add(genericCode, $"{role} chain build threw {ex.GetType().Name}: {ex.Message}");
            return;
        }

        try
        {
            InterpretChainStatus(chain, ok, kind, result);
        }
        finally
        {
            foreach (X509ChainElement element in chain.ChainElements)
            {
                element.Certificate.Dispose();
            }
        }
    }

    /// <summary>
    /// Sets up <see cref="X509ChainPolicy"/>: <see cref="X509ChainTrustMode.CustomRootTrust"/>
    /// against ONLY the explicitly-supplied <paramref name="customRoots"/> (the pinned PEMs
    /// in <c>codesignctl.pem</c> / <c>timestampctl.pem</c>)
    /// ntire-chain revocation, EKU enforcement at chain-build time
    /// (defense-in-depth on top of the leaf-only EKU bag check), and a strict
    /// <see cref="X509VerificationFlags.NoFlag"/> (release manifests are FRESH artifacts so we
    /// surface NotTimeValid as a failure rather than ignoring it).
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
    }

    /// <summary>
    /// Maps post-build <see cref="X509Chain.ChainStatus"/> flags to the role-specific
    /// <see cref="FailureCode"/>s. Revoked &gt; offline-revocation &gt; generic build failure.
    /// </summary>
    private static void InterpretChainStatus(
        X509Chain chain,
        bool buildSucceeded,
        ChainKind kind,
        VerificationResult result)
    {
        if (buildSucceeded && chain.ChainStatus.Length == 0)
        {
            return;
        }

        var (role, timeoutCode, revokedCode, genericCode) = GetChainCodes(kind);

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
