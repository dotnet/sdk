// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal.Signing;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for <see cref="SignatureVerifier"/>. The verifier is collect-all (it never
/// short-circuits), so these tests assert that specific <see cref="FailureCode"/>s are or are
/// not present in the result rather than asserting overall validity. That lets us exercise
/// every spec check on real-world fixtures without needing a perfect signed-and-still-valid
/// blob whose chain we can build hermetically.
///
/// <para>Fixtures (under TestAssets/Signing/):</para>
/// <list type="bullet">
///   <item><c>releases-directory.json</c> + <c>releases-directory.json.20260505084330.p7s</c> —
///         a real .NET release-metadata signature produced by the Microsoft signer the verifier
///         is pinned to. Subject DN, issuer DN, EKU, CMS shape, RSA, SHA-384 digest, RFC 3161
///         timestamp, and JSON expiration policy all match the spec on this pair.</item>
///   <item><c>vscode-runtime.signature.p7s</c> — a CMS signature from a different (non-.NET-Release)
///         signer, used to drive the negative subject/issuer DN paths.</item>
/// </list>
/// </summary>
public class SignatureVerifierTests
{
    private static readonly string s_signingAssetsDir = Path.Combine(
        typeof(SignatureVerifierTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "TestAssetsDir").Value!,
        "Signing");

    private static byte[] LoadAsset(string name) =>
        File.ReadAllBytes(Path.Combine(s_signingAssetsDir, name));

    /// <summary>
    /// Returns the same trust roots dnup ships in production (the SDK's
    /// <c>codesignctl.pem</c> / <c>timestampctl.pem</c>, embedded as resources). We do NOT
    /// substitute self-signed fixtures because the spec pins the signer DN to the real
    /// "Microsoft Corporation, OU=.NET Release" identity, which we cannot fabricate.
    /// </summary>
    private static SignatureVerificationOptions ProductionOptions(bool requireExpiration = true) =>
        new(TrustedRootsLoader.CodeSigningRoots, TrustedRootsLoader.TimestampRoots)
        {
            RevocationMode = RevocationCheckMode.NoCheck, // tests are hermetic; CRL/OCSP unreachable
            RequireJsonExpirationField = requireExpiration,
        };

    private static SignatureVerificationOptions OptionsWithEmptyRoots() =>
        new(new X509Certificate2Collection(), new X509Certificate2Collection());

    /// <summary>
    /// Verification-time pin: the timestamp baked into the fixture's RFC 3161 token is around
    /// May 5, 2026. The signature.expiration is 2026-08-03. Pinning to mid-July keeps the
    /// "ExpiredNow" check from firing while the cached fixtures age in the repo.
    /// </summary>
    private static readonly DateTimeOffset s_pinnedNow = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    // ---------------- Argument validation ----------------

    [Fact]
    public void Verify_NullContent_Throws()
    {
        Action act = () => SignatureVerifier.Verify(null!, new byte[] { 1 }, ProductionOptions());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Verify_NullSignature_Throws()
    {
        Action act = () => SignatureVerifier.Verify(new byte[] { 1 }, null!, ProductionOptions());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Verify_NullOptions_Throws()
    {
        Action act = () => SignatureVerifier.Verify(new byte[] { 1 }, new byte[] { 1 }, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---------------- Trusted-roots policy ----------------

    [Fact]
    public void Verify_EmptyTrustedRoots_FlagsTrustedRootsEmpty()
    {
        // Use the real fixture but with empty root collections — the verifier must report
        // the empty-roots condition explicitly so misconfigured callers don't silently fall
        // back to an OS-only chain build.
        var options = OptionsWithEmptyRoots();
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            options,
            s_pinnedNow);

        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.TrustedRootsEmpty);
    }

    // ---------------- CMS shape and crypto ----------------

    [Fact]
    public void Verify_GarbageSignature_FlagsSigDecodeFailed()
    {
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            new byte[] { 0x00, 0x01, 0x02, 0x03 },
            ProductionOptions(requireExpiration: false),
            s_pinnedNow);

        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.SigDecodeFailed);
    }

    [Fact]
    public void Verify_TamperedContent_FlagsSigCryptoInvalid()
    {
        // Flip a single byte in the middle of the JSON — the embedded message digest no
        // longer matches and CheckSignature() must reject.
        byte[] tampered = LoadAsset("releases-directory.json");
        int idx = tampered.Length / 2;
        tampered[idx] = (byte)(tampered[idx] ^ 0xFF);

        var result = SignatureVerifier.Verify(
            tampered,
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow);

        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.SigCryptoInvalid);
    }

    [Fact]
    public void Verify_AuthenticReleasesManifest_PassesAllSignerDNAndAlgorithmChecks()
    {
        // The fixture is real so it MUST pass every check that doesn't depend on hitting
        // a live CRL/OCSP endpoint. We can't assert IsValid because the chain build will
        // still report status under NoCheck for OS-store gaps in CI; instead, assert that
        // none of the DN/EKU/algorithm/CMS-shape/timestamp-shape failures fired.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(),
            s_pinnedNow);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();

        // Cryptographic integrity — pinned bytes really do verify against the pinned sig.
        codes.Should().NotContain(FailureCode.SigCryptoInvalid);
        codes.Should().NotContain(FailureCode.SigDecodeFailed);
        codes.Should().NotContain(FailureCode.SigNotCms);
        codes.Should().NotContain(FailureCode.SigMultipleSigners);
        codes.Should().NotContain(FailureCode.SignerCertMissing);

        // Algorithm policy.
        codes.Should().NotContain(FailureCode.WeakDigest);
        codes.Should().NotContain(FailureCode.WeakSignatureAlgorithm);

        // Signer cert policy (the whole point of the pinning).
        codes.Should().NotContain(FailureCode.SubjectMismatch);
        codes.Should().NotContain(FailureCode.IssuerMismatch);
        codes.Should().NotContain(FailureCode.EkuMissing);
        codes.Should().NotContain(FailureCode.EkuNotExclusiveCodeSign);

        // Timestamp shape.
        codes.Should().NotContain(FailureCode.TimestampMissing);
        codes.Should().NotContain(FailureCode.TimestampMalformed);
        codes.Should().NotContain(FailureCode.TimestampBindingInvalid);
        codes.Should().NotContain(FailureCode.TimestampEkuInvalid);

        // JSON expiration policy.
        codes.Should().NotContain(FailureCode.JsonParseFailed);
        codes.Should().NotContain(FailureCode.ExpirationMissing);
        codes.Should().NotContain(FailureCode.ExpirationMalformed);
        codes.Should().NotContain(FailureCode.SignedAfterExpiration);
        codes.Should().NotContain(FailureCode.ExpiredNow);
    }

    [Fact]
    public void Verify_AuthenticManifest_AfterExpiration_FlagsExpiredNow()
    {
        // signature.expiration in the v3 fixture is 2026-08-03. Pin "now" past that.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.ExpiredNow);
    }

    [Fact]
    public void Verify_NonObjectJsonContent_FlagsExpirationMissing()
    {
        // The verifier requires an object root; a JSON array trips ExpirationMissing
        // (the sig is also wrong but the checks are independent).
        byte[] arrayContent = System.Text.Encoding.UTF8.GetBytes("[1,2,3]");

        var result = SignatureVerifier.Verify(
            arrayContent,
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(),
            s_pinnedNow);

        // Both will fire; we care that expiration check ran (collect-all behavior).
        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.ExpirationMissing);
    }

    [Fact]
    public void Verify_RequireExpirationFalse_SkipsJsonPolicyEntirely()
    {
        // Same garbage content as above, but turn RequireJsonExpirationField off — none
        // of the JSON-policy failure codes should appear regardless of content shape.
        byte[] arrayContent = System.Text.Encoding.UTF8.GetBytes("[1,2,3]");

        var result = SignatureVerifier.Verify(
            arrayContent,
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();
        codes.Should().NotContain(FailureCode.ExpirationMissing);
        codes.Should().NotContain(FailureCode.ExpirationMalformed);
        codes.Should().NotContain(FailureCode.SignedAfterExpiration);
        codes.Should().NotContain(FailureCode.ExpiredNow);
        codes.Should().NotContain(FailureCode.JsonParseFailed);
    }

    // ---------------- Foreign signer (negative DN/issuer/EKU paths) ----------------

    [Fact]
    public void Verify_ForeignSigner_FlagsSubjectMismatch()
    {
        // The vscode-dotnet-runtime CMS signature is signed by a different Microsoft entity
        // (CN=Microsoft Corporation without OU=.NET Release). Even though we don't have its
        // matching content (so SigCryptoInvalid will also fire), the subject DN check runs
        // against the embedded signer cert independently and must reject.
        //
        // We don't assert IssuerMismatch here: the vscode signer happens to chain through
        // the same DigiCert "Trusted G4 Code Signing RSA4096 SHA384 2021 CA1" intermediate
        // that .NET Release uses (signed in the same era), so the issuer pin (spec §5.2)
        // legitimately passes. Use the 3.1.x NuGet sig fixture below for IssuerMismatch.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),  // wrong content — that's fine, checks are independent
            LoadAsset("vscode-runtime.signature.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow);

        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.SubjectMismatch);
    }

    [Fact]
    public void Verify_NuGetPackageSignature_FromOlderIntermediate_FlagsIssuerMismatch()
    {
        // Microsoft.AspNetCore.App.Runtime 3.1.32's NuGet .signature.p7s is signed by
        // CN=Microsoft Corporation (no OU=.NET Release) chaining through the older
        // "DigiCert SHA2 Assured ID Code Signing CA" intermediate — NOT the
        // "DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1" pin from spec §5.2.
        // This is the one realistic Microsoft fixture that drives IssuerMismatch.
        //
        // Using a NuGet .signature.p7s as the signature with our JSON content guarantees
        // SigCryptoInvalid (the sig was computed over the package signature manifest, not
        // our JSON) — but the verifier's collect-all behavior means SubjectMismatch and
        // IssuerMismatch checks run independently against the embedded signer cert.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            LoadAsset("nuget-aspnetcore-3.1.32.signature.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();
        codes.Should().Contain(FailureCode.IssuerMismatch, "the older DigiCert intermediate is not the spec §5.2 pin");
        codes.Should().Contain(FailureCode.SubjectMismatch, "the NuGet signer subject lacks the required OU=.NET Release");
    }

    [Fact]
    public void Verify_NuGetPackageSignature_PassesAlgorithmAndEkuChecks()
    {
        // Same NuGet fixture confirms the algorithm + EKU checks DON'T over-fire on a
        // legitimate Microsoft NuGet signer: SHA-256 digest is allowed (not WeakDigest),
        // RSA public key is allowed (not WeakSignatureAlgorithm), and the EKU is exactly
        // id-kp-codeSigning (not EkuMissing or EkuNotExclusiveCodeSign).
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            LoadAsset("nuget-aspnetcore-3.1.32.signature.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();
        codes.Should().NotContain(FailureCode.WeakDigest);
        codes.Should().NotContain(FailureCode.WeakSignatureAlgorithm);
        codes.Should().NotContain(FailureCode.EkuMissing);
        codes.Should().NotContain(FailureCode.EkuNotExclusiveCodeSign);
        codes.Should().NotContain(FailureCode.SigMultipleSigners);
        codes.Should().NotContain(FailureCode.SignerCertMissing);
    }

    // ---------------- Collect-all behavior ----------------

    [Fact]
    public void Verify_CollectsAllFailures_DoesNotShortCircuit()
    {
        // Garbage signature + missing roots + non-JSON content. The verifier must report
        // multiple distinct failures rather than bailing on the first.
        var result = SignatureVerifier.Verify(
            new byte[] { 0xFF, 0xFE, 0xFD },
            new byte[] { 0xFF, 0xFE, 0xFD },
            OptionsWithEmptyRoots(),
            s_pinnedNow);

        result.Failures.Select(f => f.Code).Distinct().Count().Should().BeGreaterThan(1,
            "collect-all behavior is a contract per signature_requirements.md §10");
        result.IsValid.Should().BeFalse();
    }
}
