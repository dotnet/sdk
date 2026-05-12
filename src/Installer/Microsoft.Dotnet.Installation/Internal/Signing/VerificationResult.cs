// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// Stable identifiers for every spec-violation the <see cref="SignatureVerifier"/> can emit.
/// Mirrors the failure codes documented in
/// <c>detached_signature_validation/signature_requirements.md</c>.
/// </summary>
internal enum FailureCode
{
    None = 0,

    // Inputs
    ContentFileMissing,
    SignatureFileMissing,
    TrustedRootsNotFound,
    TrustedRootsEmpty,

    // CMS shape
    SigNotCms,
    SigDecodeFailed,
    SigMultipleSigners,
    SignerCertMissing,

    // Cryptography
    SigCryptoInvalid,
    WeakDigest,
    WeakSignatureAlgorithm,

    // Signed attributes
    ContentTypeAttributeInvalid,
    MessageDigestMismatch,
    SigningTimeMissing,
    SigningTimeMismatch,

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
/// Aggregated verification result. The verifier collects all failures rather than stopping at
/// the first one so callers can surface every spec violation in a single error message.
/// </summary>
internal sealed class VerificationResult
{
    private readonly List<VerificationFailure> _failures = new();

    public IReadOnlyList<VerificationFailure> Failures => _failures;
    public bool IsValid => _failures.Count == 0;

    public void Add(FailureCode code, string reason) => _failures.Add(new VerificationFailure(code, reason));

    public void AddSkip(string reason) => _failures.Add(new VerificationFailure(FailureCode.CheckSkipped, reason));
}
