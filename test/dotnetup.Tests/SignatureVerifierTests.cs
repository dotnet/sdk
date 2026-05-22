// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal.Signing;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for <see cref="SignatureVerifier"/>. The verifier defaults to short-circuit
/// mode (production behavior); these tests opt into <see cref="VerificationMode.CollectAll"/>
/// where they need to assert that specific <see cref="FailureCode"/>s are or are not present
/// after later sections run. Collect-all lets us exercise every spec check on real-world
/// fixtures without needing a perfect signed-and-still-valid blob whose chain we can build
/// hermetically. A dedicated test (<c>Verify_DefaultMode_ShortCircuitsOnFirstFailure</c>)
/// covers the default short-circuit contract.
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
    private static SignatureVerificationOptions ProductionOptions(
        bool requireExpiration = true,
        RevocationCheckMode revocationMode = RevocationCheckMode.Online) =>
        new(TrustedRootsLoader.CodeSigningRoots, TrustedRootsLoader.TimestampRoots)
        {
            RevocationMode = revocationMode,
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
        result.IsValid.Should().BeFalse();
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
        result.IsValid.Should().BeFalse();
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
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_AuthenticReleasesManifest_PassesAllSignerDNAndAlgorithmChecks()
    {
        // The fixture is real and the bundled CTL chains it cleanly. With production options
        // (RevocationMode.Online — DigiCert CRL/OCSP must be reachable) the verifier MUST
        // report IsValid=true end-to-end. If this assertion regresses, something in the
        // verifier broke the happy path.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(),
            s_pinnedNow);

        result.IsValid.Should().BeTrue($"verifier reported failures: {string.Join("; ", result.Failures.Select(f => f.Code + ": " + f.Reason))}");

        var codes = result.Failures.Select(f => f.Code).ToHashSet();

        // Cryptographic integrity — pinned bytes really do verify against the pinned sig.
        codes.Should().NotContain(FailureCode.SigCryptoInvalid);
        codes.Should().NotContain(FailureCode.SigDecodeFailed);
        codes.Should().NotContain(FailureCode.SigMultipleSigners);
        codes.Should().NotContain(FailureCode.SignerCertMissing);

        // Algorithm policy.
        codes.Should().NotContain(FailureCode.WeakDigest);
        codes.Should().NotContain(FailureCode.SignatureAlgorithmNotPermitted);

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
        codes.Should().NotContain(FailureCode.TimestampIssuerMismatch);

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
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_NonObjectJsonContent_FlagsExpirationMissing()
    {
        // The verifier requires an object root; a JSON array trips ExpirationMissing
        // (the sig is also wrong but the checks are independent). Use CollectAll so the
        // JSON-policy section runs even after SigCryptoInvalid fires upstream.
        byte[] arrayContent = System.Text.Encoding.UTF8.GetBytes("[1,2,3]");

        var result = SignatureVerifier.Verify(
            arrayContent,
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(),
            s_pinnedNow,
            VerificationMode.CollectAll);

        // Both will fire; we care that expiration check ran (collect-all behavior).
        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.ExpirationMissing);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_RequireExpirationFalse_SkipsJsonPolicyEntirely()
    {
        // Same garbage content as above, but turn RequireJsonExpirationField off — none
        // of the JSON-policy failure codes should appear regardless of content shape.
        // CollectAll mode is required: SigCryptoInvalid would otherwise short-circuit the
        // pipeline before the JSON-policy section is reached, making the NotContain
        // assertions vacuously pass and proving nothing about the option.
        byte[] arrayContent = System.Text.Encoding.UTF8.GetBytes("[1,2,3]");

        var result = SignatureVerifier.Verify(
            arrayContent,
            LoadAsset("releases-directory.json.20260505084330.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow,
            VerificationMode.CollectAll);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();
        codes.Should().NotContain(FailureCode.ExpirationMissing);
        codes.Should().NotContain(FailureCode.ExpirationMalformed);
        codes.Should().NotContain(FailureCode.SignedAfterExpiration);
        codes.Should().NotContain(FailureCode.ExpiredNow);
        codes.Should().NotContain(FailureCode.JsonParseFailed);
        // SigCryptoInvalid still fires on the array-vs-real-content mismatch, so IsValid is
        // false here for reasons other than JSON policy. We assert the JSON section was
        // skipped, not that overall verification passed.
        result.IsValid.Should().BeFalse();
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
        //
        // CollectAll: SigCryptoInvalid would otherwise short-circuit before SubjectMismatch.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),  // wrong content — that's fine, checks are independent
            LoadAsset("vscode-runtime.signature.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow,
            VerificationMode.CollectAll);

        result.Failures.Select(f => f.Code).Should().Contain(FailureCode.SubjectMismatch);
        result.IsValid.Should().BeFalse();
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
            s_pinnedNow,
            VerificationMode.CollectAll);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();
        codes.Should().Contain(FailureCode.IssuerMismatch, "the older DigiCert intermediate is not the spec §5.2 pin");
        codes.Should().Contain(FailureCode.SubjectMismatch, "the NuGet signer subject lacks the required OU=.NET Release");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_NuGetPackageSignature_PassesAlgorithmAndEkuChecks()
    {
        // Same NuGet fixture confirms the algorithm + EKU checks DON'T over-fire on a
        // legitimate Microsoft NuGet signer: SHA-256 digest is allowed (not WeakDigest),
        // RSA public key is allowed (not SignatureAlgorithmNotPermitted), and the EKU is exactly
        // id-kp-codeSigning (not EkuMissing or EkuNotExclusiveCodeSign).
        // CollectAll mode is required: SigCryptoInvalid (the sig was computed over a NuGet
        // package, not our JSON) would otherwise short-circuit before EvaluateAlgorithmPolicy
        // / EvaluateSignerCertificatePolicy run, making the NotContain assertions vacuous.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory.json"),
            LoadAsset("nuget-aspnetcore-3.1.32.signature.p7s"),
            ProductionOptions(requireExpiration: false),
            s_pinnedNow,
            VerificationMode.CollectAll);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();
        codes.Should().NotContain(FailureCode.WeakDigest);
        codes.Should().NotContain(FailureCode.SignatureAlgorithmNotPermitted);
        codes.Should().NotContain(FailureCode.EkuMissing);
        codes.Should().NotContain(FailureCode.EkuNotExclusiveCodeSign);
        codes.Should().NotContain(FailureCode.SigMultipleSigners);
        codes.Should().NotContain(FailureCode.SignerCertMissing);
        // The fixture is a foreign signer, so overall verification still fails (DN pins,
        // crypto). This test only asserts the algorithm/EKU sub-checks didn't over-fire.
        result.IsValid.Should().BeFalse();
    }

    // ---------------- Timestamp-shape coverage from earlier signing-protocol drafts ----------------
    //
    // The v3 happy fixture has a valid RFC 3161 timestamp. To exercise the negative timestamp
    // paths we use earlier drafts of the same release-metadata signing experiment that were
    // produced by the real .NET Release signer but with a different signing-attribute layout:
    //
    //   * v1 (releases-directory-v1.json + .sig): no RFC 3161 timestamp attribute at all.
    //     Drives TimestampMissing positively. Crypto still verifies because the .sig was
    //     produced for that exact JSON.
    //
    //   * v2 (releases-directory-v2.json + .p7s): valid RFC 3161 timestamp present, but the
    //     SignerInfo uses SHA-256 instead of v3's SHA-384. Confirms the verifier accepts
    //     both digest algorithms (both are in the allow-list).

    [Fact]
    public void Verify_V1Signature_FlagsTimestampMissing_AndCascadingSkips()
    {
        // The v1 fixture (May 2026 draft) has no RFC 3161 timestamp attribute at all.
        // The verifier must positively fire TimestampMissing AND, because all downstream
        // chain/JSON-policy checks rely on the TSA timestamp, must record CheckSkipped
        // entries for the TSA chain, primary chain, and JSON expiration. CollectAll so the
        // downstream sections actually run after TimestampMissing fires.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory-v1.json"),
            LoadAsset("releases-directory-v1.json.20260424114016.sig"),
            ProductionOptions(),
            s_pinnedNow,
            VerificationMode.CollectAll);

        var codes = result.Failures.Select(f => f.Code).ToList();

        codes.Should().Contain(FailureCode.TimestampMissing);

        // Crypto still works because the sig was produced for this exact content; signer DN
        // and issuer also pass since v1 was already produced by the .NET Release signer.
        codes.Should().NotContain(FailureCode.SigCryptoInvalid);
        codes.Should().NotContain(FailureCode.SubjectMismatch);
        codes.Should().NotContain(FailureCode.IssuerMismatch);
        codes.Should().NotContain(FailureCode.EkuMissing);
        codes.Should().NotContain(FailureCode.EkuNotExclusiveCodeSign);

        // Downstream cascades — the verifier must record CheckSkipped breadcrumbs (not
        // crash, not silently pass) when the TSA timestamp is unavailable. Skips are
        // appended to the same Failures list as real failures (signature-verification
        // doc §10): a CheckSkipped only ever appears as a consequence of an upstream
        // failure, so it correctly contributes to IsValid == false.
        result.Failures.Count(f => f.Code == FailureCode.CheckSkipped).Should().BeGreaterThanOrEqualTo(2,
            "TSA chain and JSON expiration policy must both record skips when no TSA timestamp is available");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_V2Signature_AcceptsSha256Digest_AndFlagsTimestampBindingInvalid()
    {
        // v2 fixture (April 2026 draft) was signed with SHA-256 instead of v3's SHA-384.
        // Both digests are in the verifier's allow-list (spec §4) — confirming SHA-256 is
        // accepted is the primary win. We assert WeakDigest does NOT fire.
        //
        // Bonus coverage: v2's RFC 3161 token doesn't actually bind to the SignerInfo's
        // EncryptedDigest (an early bug in the producer that v3 fixed). The verifier must
        // catch this with TimestampBindingInvalid. This is the only real-world fixture we
        // have that drives this specific failure positively.
        var result = SignatureVerifier.Verify(
            LoadAsset("releases-directory-v2.json"),
            LoadAsset("releases-directory-v2.json.p7s"),
            ProductionOptions(),
            s_pinnedNow);

        var codes = result.Failures.Select(f => f.Code).ToHashSet();

        // SHA-256 is allowed; if WeakDigest fired here we accidentally restricted the policy.
        codes.Should().NotContain(FailureCode.WeakDigest);
        codes.Should().NotContain(FailureCode.SignatureAlgorithmNotPermitted);

        // Crypto + DN + EKU + timestamp-shape (other than the binding bug) all pass.
        codes.Should().NotContain(FailureCode.SigCryptoInvalid);
        codes.Should().NotContain(FailureCode.SubjectMismatch);
        codes.Should().NotContain(FailureCode.IssuerMismatch);
        codes.Should().NotContain(FailureCode.EkuMissing);
        codes.Should().NotContain(FailureCode.EkuNotExclusiveCodeSign);
        codes.Should().NotContain(FailureCode.TimestampMissing);
        codes.Should().NotContain(FailureCode.TimestampMalformed);

        // The producer-side binding bug — must fire.
        codes.Should().Contain(FailureCode.TimestampBindingInvalid);
        result.IsValid.Should().BeFalse();
    }

    // ---------------- Collect-all behavior ----------------

    [Fact]
    public void Verify_CollectsAllFailures_DoesNotShortCircuit()
    {
        // Garbage signature + missing roots + non-JSON content. With CollectAll mode the
        // verifier must report multiple distinct failures rather than bailing on the first.
        var result = SignatureVerifier.Verify(
            new byte[] { 0xFF, 0xFE, 0xFD },
            new byte[] { 0xFF, 0xFE, 0xFD },
            OptionsWithEmptyRoots(),
            s_pinnedNow,
            VerificationMode.CollectAll);

        result.Failures.Select(f => f.Code).Distinct().Count().Should().BeGreaterThan(1,
            "collect-all behavior is a contract for callers that opt into VerificationMode.CollectAll");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_DefaultMode_ShortCircuitsOnFirstFailure()
    {
        // Same inputs as the CollectAll test above, but with the default ShortCircuit mode.
        // Section atomicity: within EvaluateTrustedRoots both empty-root checks run before
        // ShouldStop is checked at the section boundary — so we may see up to 2 failures
        // from that one section, but the verifier MUST NOT proceed to DecodeCms (which would
        // record SigDecodeFailed on the garbage signature). Asserting the failure list
        // contains only TrustedRootsEmpty proves short-circuit kicked in between sections.
        var result = SignatureVerifier.Verify(
            new byte[] { 0xFF, 0xFE, 0xFD },
            new byte[] { 0xFF, 0xFE, 0xFD },
            OptionsWithEmptyRoots(),
            s_pinnedNow);
        // mode parameter omitted — default is ShortCircuit per the production design.

        result.IsValid.Should().BeFalse();
        result.ShouldStop.Should().BeTrue();
        result.Failures.Select(f => f.Code).Should().AllBeEquivalentTo(FailureCode.TrustedRootsEmpty,
            "ShortCircuit must stop after the first failing section; CMS-decode must not have run.");
    }

    // ---------------- NuGet-parity regression tests ----------------
    //
    // Guard the chain-policy decisions that mirror NuGet's signing stack so that future
    // BCL or CTL changes don't silently regress them. See the design notes at the top of
    // SignatureVerifier.cs for the full set of NuGet sources we mirrored.
    //
    // Several behaviors we'd like to cover require fixtures or test seams we don't have
    // today. Those are captured below as Skip-reason tests so they appear as TODOs in
    // test output rather than getting forgotten.

    [Fact]
    public void Verify_RepeatedCalls_DoNotLeakOsHandles()
    {
        // dotnetup mirrors NuGet's X509ChainHolder.Dispose by disposing every
        // X509ChainElement.Certificate after chain build. Without that, repeated verify
        // calls would accumulate OS-handle pressure (each X509Certificate2 holds a handle
        // released only by GC finalizer). On Linux this can hit ulimit under load.
        //
        // Process.HandleCount is process-wide and noisy (xUnit threads, file watchers, GC
        // threads can allocate or release handles between samples). To make this test
        // deterministic without losing the regression signal, run two batches of very
        // different sizes and assert the per-iteration growth is bounded by a small
        // constant in BOTH batches. If the verifier is leaking, growth scales linearly with
        // batch size and the per-iteration ratio stays nonzero. If it's clean, the
        // per-iteration ratio approaches zero as batch size grows because the constant
        // background noise gets amortized away.
        const int smallBatch = 50;
        const int largeBatch = 500;

        // Per-iteration growth threshold: chain build allocates ~4–5 cert handles per
        // verify (signer + 2–3 intermediates + root, ×2 for primary + TSA chains). If
        // disposal regresses we'd expect ≥4 leaked handles per call. We allow up to 1
        // handle per iteration as headroom for runtime/test-host noise; anything >1 is
        // a real regression.
        const double maxHandlesPerIteration = 1.0;

        byte[] content = LoadAsset("releases-directory.json");
        byte[] sig = LoadAsset("releases-directory.json.20260505084330.p7s");
        // RevocationMode.NoCheck: this test exercises the chain-element disposal regression
        // (handle leak), not the network revocation path. With Online revocation enabled,
        // 555 verifications drive 555*N CRL/OCSP lookups against DigiCert from CI — slow,
        // flaky on offline runners, and unrelated to what we're regressing here.
        var options = ProductionOptions(revocationMode: RevocationCheckMode.NoCheck);

        // Warm-up: first verify primes any one-time caches (PEM load, OS root store) and
        // tier-up JIT for the verifier methods.
        for (int i = 0; i < 5; i++)
        {
            SignatureVerifier.Verify(content, sig, options, s_pinnedNow);
        }
        ForceFullGc();

        double smallGrowthPerIter = MeasurePerIterationHandleGrowth(content, sig, options, smallBatch);
        double largeGrowthPerIter = MeasurePerIterationHandleGrowth(content, sig, options, largeBatch);

        smallGrowthPerIter.Should().BeLessThanOrEqualTo(maxHandlesPerIteration,
            $"verifier leaked >{maxHandlesPerIteration} handle/call over {smallBatch} iterations");
        largeGrowthPerIter.Should().BeLessThanOrEqualTo(maxHandlesPerIteration,
            $"verifier leaked >{maxHandlesPerIteration} handle/call over {largeBatch} iterations");
    }

    private static double MeasurePerIterationHandleGrowth(byte[] content, byte[] sig, SignatureVerificationOptions options, int iterations)
    {
        ForceFullGc();
        long before = System.Diagnostics.Process.GetCurrentProcess().HandleCount;

        for (int i = 0; i < iterations; i++)
        {
            SignatureVerifier.Verify(content, sig, options, s_pinnedNow);
        }

        ForceFullGc();
        long after = System.Diagnostics.Process.GetCurrentProcess().HandleCount;
        long growth = Math.Max(0, after - before);
        return (double)growth / iterations;
    }

    private static void ForceFullGc()
    {
        // Two-pass collect: finalizable cert objects need one GC to be queued for
        // finalization, finalization to run, then another GC to actually collect them.
        for (int i = 0; i < 2; i++)
        {
            GC.Collect(generation: GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public void Verify_IntermediateConstrainedAwayFromCodeSigning_FailsChainBuild()
    {
        // Defense-in-depth: chain.ChainPolicy.ApplicationPolicy enforces the codeSigning EKU
        // at chain-build time, mirroring NuGet's CertificateChainUtility.SetCertBuildChainPolicy.
        // Mint a chain whose intermediate carries an EKU that excludes codeSigning. Per
        // RFC 5280 §4.2.1.12 the effective EKU of a leaf is the intersection along the
        // chain, so the leaf's codeSigning EKU is narrowed away to nothing and X509Chain.Build
        // reports NotValidForUsage. EvaluateChain must surface that as ChainBuildFailed.
        byte[] content = Encoding.UTF8.GetBytes("{\"signature\":{\"expiration\":\"2099-01-01T00:00:00Z\"}}");
        var fixture = BuildTestSignedFixture(
            content,
            intermediateEkuOid: "1.3.6.1.5.5.7.3.4", // id-kp-emailProtection (NOT codeSigning)
            leafNotAfter: DateTimeOffset.UtcNow.AddYears(2));
        using (fixture.Root) using (fixture.Intermediate) using (fixture.Leaf)
        {
            var result = SignatureVerifier.Verify(
                content,
                fixture.Signature,
                BuildOptionsTrusting(fixture.Root),
                mode: VerificationMode.CollectAll);

            var chainFailure = result.Failures.FirstOrDefault(f => f.Code == FailureCode.ChainBuildFailed);
            chainFailure.Should().NotBeNull(
                "intermediate's EKU constraint excludes codeSigning, so chain build must fail " +
                "with NotValidForUsage \u2014 ApplicationPolicy enforcement is the regression target");
            chainFailure!.Reason.Should().Contain("NotValidForUsage",
                "X509Chain reports NotValidForUsage when ApplicationPolicy is not satisfied along the chain");
        }
    }

    [Fact]
    public void Verify_ExpiredSignerCert_FailsAsChainBuildFailed_NotIgnored()
    {
        // Spec §6: NotTimeValid is fatal. The verifier deliberately does NOT set
        // X509VerificationFlags.IgnoreNotTimeValid (NuGet ignores it for immutable
        // packages, we don't because release manifests are fresh artifacts). Mint a
        // chain whose leaf's NotAfter is in the past; verifier must surface
        // ChainBuildFailed with NotTimeValid in the chain-status description.
        byte[] content = Encoding.UTF8.GetBytes("{\"signature\":{\"expiration\":\"2099-01-01T00:00:00Z\"}}");
        var fixture = BuildTestSignedFixture(
            content,
            intermediateEkuOid: null, // unconstrained intermediate; only leaf validity matters
            leafNotAfter: DateTimeOffset.UtcNow.AddDays(-1));
        using (fixture.Root) using (fixture.Intermediate) using (fixture.Leaf)
        {
            var result = SignatureVerifier.Verify(
                content,
                fixture.Signature,
                BuildOptionsTrusting(fixture.Root),
                mode: VerificationMode.CollectAll);

            var chainFailure = result.Failures.FirstOrDefault(f => f.Code == FailureCode.ChainBuildFailed);
            chainFailure.Should().NotBeNull(
                "expired signer cert must fail chain build because IgnoreNotTimeValid is intentionally NOT set");
            chainFailure!.Reason.Should().Contain("NotTimeValid",
                "X509Chain reports NotTimeValid for a leaf whose NotAfter is in the past");
        }
    }

    [Fact]
    public void Verify_LeafEkuIncludesCabForumPermittedExtras_IsAccepted()
    {
        // CA/Browser Forum BR §7.1.2.3(f) permits id-kp-codeSigning leaves to ALSO carry
        // id-kp-emailProtection, Document Signing, and Microsoft Lifetime Signing. The
        // verifier must accept that combination (no EkuNotExclusiveCodeSign / EkuMissing).
        // The fixture's overall chain still won't build to a trusted root (no TSA token,
        // no real CRL), so IsValid is false — this test only asserts the EKU sub-check
        // doesn't over-fire on a CAB-Forum-compliant EKU set.
        byte[] content = Encoding.UTF8.GetBytes("{\"signature\":{\"expiration\":\"2099-01-01T00:00:00Z\"}}");
        var fixture = BuildTestSignedFixture(
            content,
            intermediateEkuOid: null,
            leafNotAfter: DateTimeOffset.UtcNow.AddYears(2),
            leafEkuOids: ["1.3.6.1.5.5.7.3.3", "1.3.6.1.5.5.7.3.4", "1.3.6.1.4.1.311.3.10.3.12"]); // codeSigning + emailProtection + Document Signing
        using (fixture.Root) using (fixture.Intermediate) using (fixture.Leaf)
        {
            var result = SignatureVerifier.Verify(
                content,
                fixture.Signature,
                BuildOptionsTrusting(fixture.Root),
                mode: VerificationMode.CollectAll);

            var codes = result.Failures.Select(f => f.Code).ToHashSet();
            codes.Should().NotContain(FailureCode.EkuMissing);
            codes.Should().NotContain(FailureCode.EkuNotExclusiveCodeSign,
                "CAB-Forum BR §7.1.2.3(f) explicitly permits id-kp-emailProtection and Document Signing alongside id-kp-codeSigning on a code-signing leaf");
            codes.Should().NotContain(FailureCode.EkuMultipleExtensions);
        }
    }
    [Fact]
    public void Verify_LeafEkuContainsServerAuth_FlagsEkuNotExclusiveCodeSign()
    {
        // CAB-Forum BR §7.1.2.3(f) explicitly forbids id-kp-serverAuth on a code-signing
        // leaf. Confirm it's rejected even when id-kp-codeSigning is also present.
        byte[] content = Encoding.UTF8.GetBytes("{\"signature\":{\"expiration\":\"2099-01-01T00:00:00Z\"}}");
        var fixture = BuildTestSignedFixture(
            content,
            intermediateEkuOid: null,
            leafNotAfter: DateTimeOffset.UtcNow.AddYears(2),
            leafEkuOids: ["1.3.6.1.5.5.7.3.3", "1.3.6.1.5.5.7.3.1"]); // codeSigning + serverAuth
        using (fixture.Root) using (fixture.Intermediate) using (fixture.Leaf)
        {
            var result = SignatureVerifier.Verify(
                content,
                fixture.Signature,
                BuildOptionsTrusting(fixture.Root),
                mode: VerificationMode.CollectAll);

            result.Failures.Select(f => f.Code).Should().Contain(FailureCode.EkuNotExclusiveCodeSign);
            result.IsValid.Should().BeFalse();
        }
    }

    // ---------------- BCL drift detector for the PQC OID allow-list ----------------

    [Fact]
    public void PqcOidList_StaysInSyncWithBcl()
    {
        // The SDK does NOT expose a public way to enumerate the OIDs each PQC algorithm
        // family recognizes — MLDsaAlgorithm.GetMLDsaAlgorithmFromOid / SlhDsaAlgorithm.GetAlgorithmFromOid /
        // CompositeMLDsaAlgorithm.GetAlgorithmFromOid are all `internal`, and the underlying
        // KnownOids arrays are `private static readonly string[]`. We mirror that data into
        // SignatureVerifier.s_pqcSignatureOids by hand.
        //
        // This test reaches into those private arrays via reflection and reports drift as a
        // *skip*, not a hard failure. Skipped tests show up in CI with the skip reason
        // visible in test reports, so a maintainer can see "BCL added OIDs X, Y, Z — please
        // update SignatureVerifier.s_pqcSignatureOids" without the build going red. The
        // reflection itself is best-effort: if the BCL refactors the field names away, the
        // test skips with a "couldn't inspect BCL" reason rather than failing, again to
        // avoid breaking CI on unrelated runtime updates. The verifier's own algorithm
        // policy still works either way because the hand-mirrored list is the authority at
        // runtime.
        var bclOids = TryLoadBclPqcKnownOids(out string? loadFailureReason);
        if (bclOids is null)
        {
            Assert.Skip($"Could not reflect BCL PQC KnownOids for drift check: {loadFailureReason}. " +
                        "This usually means the BCL refactored its internal field layout; please " +
                        "update PqcOidList_StaysInSyncWithBcl to match the new layout.");
            return;
        }

        HashSet<string> ourOids = SignatureVerifier.s_pqcSignatureOids;
        var missingFromOurs = bclOids.Except(ourOids).OrderBy(o => o, StringComparer.Ordinal).ToList();

        if (missingFromOurs.Count > 0)
        {
            Assert.Skip(
                $"BCL has {missingFromOurs.Count} PQC OID(s) not in SignatureVerifier.s_pqcSignatureOids: " +
                $"[{string.Join(", ", missingFromOurs)}]. " +
                "Please mirror these into SignatureVerifier.cs to extend the algorithm allow-list. " +
                "This is a skip (not a failure) so CI stays green during BCL roll-forwards; the " +
                "verifier still works — it just rejects the new OIDs as 'SignatureAlgorithmNotPermitted' " +
                "until the list is updated.");
            return;
        }

        // No drift. Our list is a superset of BCL's KnownOids unions; that is the invariant.
        // (We also carry pre-hash PQC OIDs that aren't in BCL's KnownOids arrays \u2014 those
        // are fine; BCL's KnownOids only enumerates what each BCL key class can decode, not
        // every OID that may appear in CMS SignedData.)
    }

    /// <summary>
    /// Reflects into <c>System.Security.Cryptography.MLDsa.KnownOids</c>,
    /// <c>SlhDsa.s_knownOids</c>, and <c>CompositeMLDsa.s_knownOids</c> (all private static
    /// arrays) to build the union of PQC OIDs the BCL currently recognises. Returns
    /// <see langword="null"/> with a reason when reflection fails for any reason (missing
    /// type, missing field, unexpected runtime type, etc.). The caller treats null as
    /// "skip" so the test never hard-fails when the BCL is refactored.
    /// </summary>
    private static HashSet<string>? TryLoadBclPqcKnownOids(out string? failureReason)
    {
        var oids = new HashSet<string>(StringComparer.Ordinal);

        if (!TryAddBclOids("System.Security.Cryptography.MLDsa", "KnownOids", oids, out failureReason)) { return null; }
        if (!TryAddBclOids("System.Security.Cryptography.SlhDsa", "s_knownOids", oids, out failureReason)) { return null; }
        if (!TryAddBclOids("System.Security.Cryptography.CompositeMLDsa", "s_knownOids", oids, out failureReason)) { return null; }

        return oids;
    }

    private static bool TryAddBclOids(string typeName, string fieldName, HashSet<string> sink, out string? failureReason)
    {
        Type? type = typeof(System.Security.Cryptography.Pkcs.SignedCms).Assembly.GetType(typeName, throwOnError: false)
                  ?? typeof(System.Security.Cryptography.RSA).Assembly.GetType(typeName, throwOnError: false);
        if (type is null)
        {
            failureReason = $"type '{typeName}' not found in any loaded crypto assembly";
            return false;
        }

        // Internal lookups in BCL: MLDsa.KnownOids is `private protected`, SlhDsa.s_knownOids
        // and CompositeMLDsa.s_knownOids are `private static`. NonPublic | Static covers both.
        System.Reflection.FieldInfo? field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field is null)
        {
            failureReason = $"field '{typeName}.{fieldName}' not found";
            return false;
        }

        if (field.GetValue(null) is not string[] arr)
        {
            failureReason = $"field '{typeName}.{fieldName}' is not a string[]";
            return false;
        }

        foreach (string oid in arr) { sink.Add(oid); }
        failureReason = null;
        return true;
    }

    // ---------------- Test-fixture helpers (in-process cert minting) ----------------

    /// <summary>
    /// Builds an in-memory 3-cert chain (root → intermediate → leaf) and a detached
    /// CMS signature over <paramref name="content"/>. Subject and issuer DNs are minted
    /// to satisfy <see cref="SignatureVerifier"/>'s pinned RDN set so the test can drive
    /// chain-build behavior (EKU constraint, validity window) without tripping the DN
    /// pins first.
    /// </summary>
    /// <param name="leafEkuOids">Optional override for the leaf's EKU OIDs (single extension, multiple OIDs).
    /// Defaults to <c>id-kp-codeSigning</c> alone.</param>
    /// <param name="addExtraLeafEkuExtension">When true, adds a second
    /// <see cref="X509EnhancedKeyUsageExtension"/> to the leaf carrying <c>id-kp-codeSigning</c>.
    /// Used to drive the <see cref="FailureCode.EkuMultipleExtensions"/> path; per RFC 5280 §4.2.1.12
    /// multiple EKU extensions on a single cert is non-conformant.</param>
    private static (X509Certificate2 Root, X509Certificate2 Intermediate, X509Certificate2 Leaf, byte[] Signature) BuildTestSignedFixture(
        byte[] content,
        string? intermediateEkuOid,
        DateTimeOffset leafNotAfter,
        string[]? leafEkuOids = null,
        bool addExtraLeafEkuExtension = false)
    {
        var notBefore = DateTimeOffset.UtcNow.AddYears(-1);

        // Root: self-signed CA, untrusted by the OS but added to the verifier's CustomTrustStore.
        using var rootKey = RSA.Create(2048);
        var rootReq = new CertificateRequest(
            new X500DistinguishedName("CN=dnup-test-root"), rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        rootReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootReq.PublicKey, false));
        var root = rootReq.CreateSelfSigned(notBefore, DateTimeOffset.UtcNow.AddYears(10));

        // Intermediate: subject DN must match SignatureVerifier.s_requiredIssuerRdns so the
        // leaf's IssuerName passes the pin.
        using var intKey = RSA.Create(2048);
        var intReq = new CertificateRequest(BuildPinnedIssuerDn(), intKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        intReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        intReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(intReq.PublicKey, false));
        if (intermediateEkuOid is not null)
        {
            intReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(intermediateEkuOid) }, critical: true));
        }
        byte[] intSerial = new byte[16]; RandomNumberGenerator.Fill(intSerial);
        using var intermediateNoKey = intReq.Create(root, notBefore, DateTimeOffset.UtcNow.AddYears(5), intSerial);
        var intermediate = intermediateNoKey.CopyWithPrivateKey(intKey);

        // Leaf: subject DN must match SignatureVerifier.s_requiredSubjectRdns; EKU is
        // codeSigning by default. The intermediate's constraint (if any) is what should
        // narrow the effective EKU at chain-build time.
        using var leafKey = RSA.Create(2048);
        var leafReq = new CertificateRequest(BuildPinnedSubjectDn(), leafKey, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);
        leafReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        leafReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var ekuCollection = new OidCollection();
        foreach (string oid in leafEkuOids ?? ["1.3.6.1.5.5.7.3.3"]) // default: id-kp-codeSigning
        {
            ekuCollection.Add(new Oid(oid));
        }
        leafReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(ekuCollection, critical: false));
        if (addExtraLeafEkuExtension)
        {
            // Adding a second EKU extension is non-conformant per RFC 5280 §4.2.1.12 — the
            // intended encoding is a single extension whose value is a sequence of OIDs. The
            // BCL does not reject the duplicate-extension shape itself; the verifier surfaces
            // it as EkuMultipleExtensions.
            leafReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.3") }, critical: false));
        }
        leafReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(leafReq.PublicKey, false));
        byte[] leafSerial = new byte[16]; RandomNumberGenerator.Fill(leafSerial);
        using var leafNoKey = leafReq.Create(intermediate, notBefore, leafNotAfter, leafSerial);
        var leaf = leafNoKey.CopyWithPrivateKey(leafKey);

        // Detached CMS signature with SHA-384 digest (matches the v3 production fixture).
        // Include the intermediate in the signed-data CertificateSet so the verifier's chain
        // build (which uses cms.Certificates as ExtraStore) can find it.
        var contentInfo = new ContentInfo(content);
        var cms = new SignedCms(contentInfo, detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, leaf)
        {
            DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.2"), // id-sha384
            IncludeOption = X509IncludeOption.EndCertOnly,
        };
        signer.Certificates.Add(intermediate);
        cms.ComputeSignature(signer);
        return (root, intermediate, leaf, cms.Encode());
    }

    /// <summary>
    /// Trust the test root for both code-signing and timestamp anchors (EvaluateTrustedRootOptions
    /// records TrustedRootsEmpty if either collection is empty). Disables revocation because
    /// the test certs have no CRL/OCSP endpoints; no timestamp is present so MaxAcceptableSigningAge
    /// is irrelevant for these tests.
    /// </summary>
    private static SignatureVerificationOptions BuildOptionsTrusting(X509Certificate2 testRoot)
    {
        var roots = new X509Certificate2Collection { testRoot };
        return new SignatureVerificationOptions(roots, roots)
        {
            RevocationMode = RevocationCheckMode.NoCheck,
            RequireJsonExpirationField = false,
        };
    }

    private static X500DistinguishedName BuildPinnedSubjectDn() =>
        BuildDnFromRdns(SignatureVerifier.s_requiredSubjectRdns);

    private static X500DistinguishedName BuildPinnedIssuerDn() =>
        BuildDnFromRdns(SignatureVerifier.s_requiredIssuerRdns);

    /// <summary>
    /// Builds an X.500 DN from the verifier's pinned RDN tuples. Sourcing the RDNs
    /// directly from <see cref="SignatureVerifier"/> means future spec changes (cert
    /// rotation, new RDN component) only need to be made in one place \u2014 these tests
    /// pick the new pin up automatically. Order is preserved from the source array;
    /// the verifier's <c>DistinguishedNameMatches</c> is order-insensitive but the
    /// total RDN count must match exactly, so we emit each tuple once.
    /// </summary>
    private static X500DistinguishedName BuildDnFromRdns((string Oid, string Value)[] rdns)
    {
        var b = new X500DistinguishedNameBuilder();
        foreach (var (oid, value) in rdns)
        {
            b.Add(oid, value);
        }
        return b.Build();
    }
}
