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
    private static SignatureVerificationOptions ProductionOptions(bool requireExpiration = true) =>
        new(TrustedRootsLoader.CodeSigningRoots, TrustedRootsLoader.TimestampRoots)
        {
            // Use the production default RevocationMode (Online). Tests already hit the
            // network for revocation; failing on CRL/OCSP unreachability is acceptable.
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
        // The fixture is real and the bundled CTL chains it cleanly. With
        // RevocationCheckMode.NoCheck the verifier MUST report IsValid=true end-to-end.
        // If this assertion regresses, something in the verifier broke the happy path.
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

        // Downstream cascades — the verifier must record CheckSkipped (not crash, not silently
        // pass) when the TSA timestamp is unavailable.
        codes.Where(c => c == FailureCode.CheckSkipped).Should().HaveCountGreaterThanOrEqualTo(2,
            "TSA chain and JSON expiration policy must both report CheckSkipped when no TSA timestamp is available");
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
        codes.Should().NotContain(FailureCode.WeakSignatureAlgorithm);

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
        var options = ProductionOptions();

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

    [Fact(Skip = "TODO: requires a custom cert chain whose intermediate constrains EKU away " +
                 "from code-signing. Track via dotnet/sdk issue 'dnup signing tests: mint " +
                 "EKU-constrained intermediate fixture'. Until then, ApplicationPolicy " +
                 "enforcement on chain build is covered only by manual review against NuGet's " +
                 "CertificateChainUtility.SetCertBuildChainPolicy.")]
    public void Verify_IntermediateConstrainedAwayFromCodeSigning_FailsChainBuild()
    {
    }

    [Fact(Skip = "TODO: requires a test seam in BuildWithUntrustedRootRetry, or a way to " +
                 "simulate the OS root store reporting UntrustedRoot transiently. Track via " +
                 "dotnet/sdk issue 'dnup signing tests: cover Windows UntrustedRoot retry'. " +
                 "Until then the retry path is covered only by manual review against NuGet's " +
                 "RetriableX509ChainBuildPolicy.")]
    public void Verify_TransientUntrustedRootOnWindows_RetriesAndSucceeds()
    {
    }

    [Fact(Skip = "TODO: requires a fixture signed with a now-expired cert (or a TSA timestamp " +
                 "outside the signer cert validity window). Real Microsoft .NET Release certs " +
                 "are long-lived, so this can't be triggered against production fixtures. The " +
                 "spec §6 'NotTimeValid is fatal' clause is enforced by NOT setting " +
                 "X509VerificationFlags.IgnoreNotTimeValid in EvaluateChain — confirmed by " +
                 "code review. Track via dotnet/sdk issue 'dnup signing tests: expired-cert " +
                 "fixture for NotTimeValid coverage'.")]
    public void Verify_ExpiredSignerCert_FailsAsChainBuildFailed_NotIgnored()
    {
    }
}
