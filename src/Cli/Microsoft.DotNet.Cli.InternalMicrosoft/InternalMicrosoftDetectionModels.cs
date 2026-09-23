// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Classifies whether the current environment has Microsoft-internal evidence.
/// Consumers normally create one implementation for the process lifetime.
/// </summary>
internal interface IInternalMicrosoftDetector
{
    /// <summary>
    /// Resolves one classification result or returns the cached result.
    /// </summary>
    Task<InternalMicrosoftDetectionResult> IsInternalMicrosoftMachineAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes the final classification, cache state, and provider diagnostics from one detector run.
/// </summary>
internal sealed record InternalMicrosoftDetectionResult(
    bool IsInternalMicrosoft,
    string? Source,
    string? Alias,
    string? Domain,
    bool IsCIEnvironment,
    string Outcome,
    string CacheStatus,
    TimeSpan Duration,
    IReadOnlyList<InternalMicrosoftProbeDiagnostic> ProbeDiagnostics);

/// <summary>
/// Represents one named detection operation created from a provider for a detector run.
/// </summary>
internal sealed record InternalMicrosoftProbe(
    string Name,
    Func<CancellationToken, Task<InternalMicrosoftProbeResult>> DetectAsync);

/// <summary>
/// Describes the identity evidence or bounded failure produced by one probe.
/// </summary>
internal sealed record InternalMicrosoftProbeResult(
    bool IsInternalMicrosoft,
    string? Alias,
    string? Domain,
    InternalMicrosoftProbeFailure? Failure = null)
{
    public static InternalMicrosoftProbeResult NotDetected { get; } = new(false, null, null);

    public static InternalMicrosoftProbeResult Failed(InternalMicrosoftProbeFailure failure) =>
        new(false, null, null, failure);
}

/// <summary>
/// Records non-sensitive health information for one completed probe.
/// </summary>
internal sealed record InternalMicrosoftProbeDiagnostic(
    string Source,
    string Outcome,
    TimeSpan Duration,
    bool HasAlias,
    bool HasDomain,
    InternalMicrosoftProbeFailure? Failure = null);

/// <summary>
/// Describes a probe failure without retaining exception messages or credentials.
/// </summary>
internal sealed record InternalMicrosoftProbeFailure(
    string Code,
    string Stage,
    string? ExceptionType = null,
    int? ProcessExitCode = null,
    int? HttpStatusCode = null);

/// <summary>
/// Captures the bounded output and status of one child-process probe.
/// </summary>
internal sealed record InternalMicrosoftProcessResult(
    string StandardOutput,
    string StandardError,
    int? ExitCode,
    InternalMicrosoftProbeFailure? Failure);

/// <summary>
/// Stores one process-independent classification result on disk.
/// </summary>
internal sealed record InternalMicrosoftDetectorCacheEntry(
    int Version,
    bool IsInternalMicrosoft,
    string? Source,
    string? Alias,
    string? Domain,
    bool IsCIEnvironment,
    DateTimeOffset TimestampUtc);

/// <summary>
/// Associates one completed probe with its result, diagnostic, and monotonic completion time.
/// </summary>
internal sealed record InternalMicrosoftProbeRunResult(
    InternalMicrosoftProbe Probe,
    string Source,
    InternalMicrosoftProbeResult Result,
    InternalMicrosoftProbeDiagnostic Diagnostic,
    long CompletionTimestamp);

/// <summary>
/// Describes the selected result and diagnostics from one concurrently executed provider stage.
/// </summary>
internal sealed record InternalMicrosoftProbeStageResult(
    InternalMicrosoftDetectionResult? Result,
    IReadOnlyList<InternalMicrosoftProbeDiagnostic> Diagnostics,
    bool TimedOut);

/// <summary>
/// Identifies one credential candidate by source while it remains in memory for a GitHub check.
/// </summary>
internal sealed record GitHubTokenCandidate(string Source, string Token);

/// <summary>
/// Defines stable final detector outcome values.
/// </summary>
internal static class InternalMicrosoftDetectorOutcome
{
    public const string Detected = "detected";
    public const string NotDetected = "not_detected";
    public const string Failed = "failed";
    public const string TimedOut = "timed_out";
}

/// <summary>
/// Defines stable detector cache-state values.
/// </summary>
internal static class InternalMicrosoftDetectorCacheStatus
{
    public const string Hit = "hit";
    public const string Miss = "miss";
    public const string Stale = "stale";
}

/// <summary>
/// Defines stable per-probe outcome values.
/// </summary>
internal static class InternalMicrosoftProbeOutcome
{
    public const string Detected = "detected";
    public const string NotDetected = "not_detected";
    public const string Failed = "failed";
    public const string TimedOut = "timed_out";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Defines stable, non-sensitive probe failure codes.
/// </summary>
internal static class InternalMicrosoftProbeFailureCode
{
    public const string ProcessExit = "process_exit";
    public const string ProcessTimeout = "process_timeout";
    public const string HttpTimeout = "http_timeout";
    public const string HttpStatus = "http_status";
    public const string JsonParse = "json_parse";
    public const string JsonShape = "json_shape";
    public const string RequestFailed = "request_failed";
    public const string RegistrationIncomplete = "registration_incomplete";
    public const string TenantMismatch = "tenant_mismatch";
    public const string IdentityMismatch = "identity_mismatch";
    public const string Exception = "exception";
}

/// <summary>
/// Defines stable stages for locating a probe failure.
/// </summary>
internal static class InternalMicrosoftProbeFailureStage
{
    public const string Probe = "probe";
    public const string Process = "process";
    public const string Parse = "parse";
    public const string GitHub = "github";
    public const string GitHubCandidates = "github_candidates";
    public const string GitHubUser = "github_user";
    public const string AccountStore = "account_store";
    public const string AccountStoreRecord = "account_store_record";
    public const string AccountStoreRecordStale = "account_store_record.stale";
    public const string AccountStoreRecordProperties = "account_store_record.properties";
    public const string AccountStoreRecordIdentityProvider = "account_store_record.identity_provider";
    public const string AccountStoreRecordHomeTenant = "account_store_record.home_tenant";
    public const string IdTokenPayload = "id_token_payload";
    public const string PlatformSso = "platform_sso";
    public const string PlatformSsoRegistration = "platform_sso.registration";
    public const string PlatformSsoIssuer = "platform_sso.issuer";
    public const string PlatformSsoKeyEndpoint = "platform_sso.key_endpoint";
    public const string PlatformSsoTokenEndpoint = "platform_sso.token_endpoint";
    public const string PlatformSsoIdentity = "platform_sso.identity";
}

/// <summary>
/// Supplies source-generated JSON metadata for the detector cache.
/// </summary>
[JsonSerializable(typeof(InternalMicrosoftDetectorCacheEntry))]
internal sealed partial class InternalMicrosoftDetectorJsonContext : JsonSerializerContext;
