// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// Stable identifiers for every spec-violation the <see cref="SignatureVerifier"/> can emit.
/// </summary>
internal enum FailureCode
{
    None = 0,

    // Inputs
    TrustedRootsEmpty,

    // CMS shape
    SigDecodeFailed,
    SigMultipleSigners,
    SignerCertMissing,

    // Cryptography
    SigCryptoInvalid,
    WeakDigest,
    SignatureAlgorithmNotPermitted,

    // Certificate policy
    SubjectMismatch,
    IssuerMismatch,
    EkuMissing,
    EkuNotExclusiveCodeSign,

    // Chain
    ChainBuildFailed,
    RevocationUnavailable,
    Revoked,

    // Timestamp
    TimestampMissing,
    TimestampMalformed,
    TimestampBindingInvalid,
    TimestampChainFailed,
    TimestampEkuInvalid,
    TimestampIssuerMismatch,
    TimestampRevocationUnavailable,

    // JSON policy
    JsonParseFailed,
    ExpirationMissing,
    ExpirationMalformed,
    SignedAfterExpiration,
    ExpiredNow,

    // A check could not run because a precondition failed.
    CheckSkipped,

    // Catch-all
    Unexpected,
}

internal sealed record VerificationFailure(FailureCode Code, string Reason);

/// <summary>
/// Aggregated verification result. Mutation is restricted to the verifier itself — once
/// <see cref="SignatureVerifier.Verify"/> returns, the result is effectively read-only to callers.
///
/// <para>
/// Construct with <c>shortCircuit: true</c> (the default for <see cref="SignatureVerifier.Verify"/>)
/// for production callers that only care about whether verification succeeded — <see cref="ShouldStop"/>
/// flips on the first real failure and the verifier exits without running additional checks. Construct
/// with <c>shortCircuit: false</c> for diagnostics / tests that want to see every spec violation in a
/// single run.
/// </para>
/// </summary>
internal sealed class VerificationResult
{
    private readonly List<VerificationFailure> _failures = [];
    private readonly bool _shortCircuit;

    public IReadOnlyList<VerificationFailure> Failures => _failures;
    public bool IsValid => _failures.Count == 0;

    /// <summary>
    /// True when verification should stop running additional checks. Flips on the first
    /// <see cref="Add"/> call when the result was constructed in short-circuit mode; stays
    /// false in collect-all mode. <see cref="AddSkip"/> also flips it in short-circuit mode
    /// as a defense-in-depth measure: skips are only recorded when an upstream failure
    /// already required the section to bail, so by the time a skip would fire in
    /// short-circuit mode <see cref="ShouldStop"/> should already be true. Setting it here
    /// ensures the pipeline cannot accidentally proceed past a skip in short-circuit mode
    /// even if a future change introduces a code path that records a skip without an
    /// upstream <see cref="Add"/>.
    /// </summary>
    public bool ShouldStop { get; private set; }

    public VerificationResult(bool shortCircuit = true)
    {
        _shortCircuit = shortCircuit;
    }

    internal void Add(FailureCode code, string reason)
    {
        _failures.Add(new VerificationFailure(code, reason));
        if (_shortCircuit)
        {
            ShouldStop = true;
        }
    }

    internal void AddSkip(string reason)
    {
        _failures.Add(new VerificationFailure(FailureCode.CheckSkipped, reason));
        if (_shortCircuit)
        {
            ShouldStop = true;
        }
    }
}
