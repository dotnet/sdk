// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Defines the orchestration policy for one detector.
/// Production uses the default value, while tests replace complete policy values.
/// </summary>
internal sealed record InternalMicrosoftDetectorOptions(
    Func<InternalMicrosoftDetectionContext, IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>>> CreateProbeStages,
    TimeSpan ProbeStageTimeout,
    Func<TimeSpan, CancellationTokenSource> CreateProbeStageTimeoutSource)
{
    internal static InternalMicrosoftDetectorOptions Default { get; } = new(
        CreateProbeStages: CreateDefaultProbeStages,
        ProbeStageTimeout: TimeSpan.FromSeconds(5),
        CreateProbeStageTimeoutSource: static timeout => new CancellationTokenSource(timeout));

    private static IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>> CreateDefaultProbeStages(
        InternalMicrosoftDetectionContext context) =>
        InternalMicrosoftDetector.CreateDefaultProbeStages(context);
}

/// <summary>
/// Coordinates staged providers and a shared disk cache.
/// Consumers normally create one detector instance for the process lifetime.
/// </summary>
internal sealed class InternalMicrosoftDetector : IInternalMicrosoftDetector
{
    private const int CacheVersion = 6;

    private static readonly TimeSpan s_cacheRefreshInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan s_cancelledProbeDrainTimeout = TimeSpan.FromSeconds(1);
    private static readonly IReadOnlyList<IInternalMicrosoftDetectionProvider> s_defaultProviders =
    [
        new MacPlatformSsoDetectionProvider(),
        new EnvironmentGitHubTokenDetectionProvider(),
        new GitHubCliDetectionProvider(
            "gh CLI GitHub org membership",
            "gh",
            static context => !context.IsCIEnvironment),
        new CopilotCliDetectionProvider(),
        new WindowsUserDnsDomainDetectionProvider(),
        new WindowsVisualStudioAccountDetectionProvider(),
        new WindowsWorkplaceJoinDetectionProvider(),
        new WslWindowsUserDnsDomainDetectionProvider(),
        new WslVisualStudioAccountDetectionProvider(),
        new WslWindowsWorkplaceJoinDetectionProvider(),
        new GitHubCliDetectionProvider(
            "WSL Windows gh.exe GitHub org membership",
            "gh.exe",
            static context => context.IsWsl && !context.IsCIEnvironment)
    ];

    private readonly string _cacheFilePath;
    private readonly TimeProvider _timeProvider;
    private readonly Func<InternalMicrosoftDetectionContext, IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>>> _createProbeStages;
    private readonly Func<TimeSpan, CancellationTokenSource> _createProbeStageTimeoutSource;
    private readonly TimeSpan _probeStageTimeout;
    private readonly InternalMicrosoftDetectionContext _context;

    /// <summary>
    /// Creates a detector with production platform dependencies and policy.
    /// </summary>
    internal static InternalMicrosoftDetector CreateDefault(
        string cacheFilePath,
        bool isCIEnvironment,
        string gitHubUserAgentVersion) =>
        new(
            cacheFilePath,
            TimeProvider.System,
            InternalMicrosoftDetectionContext.CreateDefault(
                InternalMicrosoftDetectionContext.GetHomeDirectory(),
                isCIEnvironment,
                gitHubUserAgentVersion),
            InternalMicrosoftDetectorOptions.Default);

    internal InternalMicrosoftDetector(
        string cacheFilePath,
        TimeProvider timeProvider,
        InternalMicrosoftDetectionContext context,
        InternalMicrosoftDetectorOptions options)
    {
        _cacheFilePath = cacheFilePath;
        _timeProvider = timeProvider;
        _context = context;
        _createProbeStages = options.CreateProbeStages;
        _probeStageTimeout = options.ProbeStageTimeout;
        _createProbeStageTimeoutSource = options.CreateProbeStageTimeoutSource;
    }

    /// <summary>
    /// Resolves one classification result and updates the process-independent cache.
    /// Concurrent callers share the cache but do not share an in-flight detector run.
    /// </summary>
    public async Task<InternalMicrosoftDetectionResult> IsInternalMicrosoftMachineAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var cached = await TryReadCacheAsync(cancellationToken).ConfigureAwait(false);
            if (cached.Entry is not null)
            {
                return FromCache(cached.Entry, stopwatch.Elapsed);
            }

            var result = await RunProbeStagesAsync(cached.CacheStatus, cancellationToken).ConfigureAwait(false);
            if (result.Outcome is InternalMicrosoftDetectorOutcome.Detected or InternalMicrosoftDetectorOutcome.NotDetected)
            {
                await TryWriteCacheAsync(result, cancellationToken).ConfigureAwait(false);
            }

            return result with { Duration = stopwatch.Elapsed };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new InternalMicrosoftDetectionResult(
                IsInternalMicrosoft: false,
                Source: null,
                Alias: null,
                Domain: null,
                IsCIEnvironment: IsCIEnvironment(),
                Outcome: InternalMicrosoftDetectorOutcome.Failed,
                CacheStatus: InternalMicrosoftDetectorCacheStatus.Miss,
                Duration: stopwatch.Elapsed,
                ProbeDiagnostics: []);
        }
    }

    private InternalMicrosoftDetectionResult FromCache(InternalMicrosoftDetectorCacheEntry entry, TimeSpan duration) =>
        new(
            entry.IsInternalMicrosoft,
            entry.Source,
            entry.Alias,
            entry.Domain,
            entry.IsCIEnvironment,
            entry.IsInternalMicrosoft ? InternalMicrosoftDetectorOutcome.Detected : InternalMicrosoftDetectorOutcome.NotDetected,
            InternalMicrosoftDetectorCacheStatus.Hit,
            duration,
            []);

    internal static IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>> CreateDefaultProbeStages(
        InternalMicrosoftDetectionContext context) =>
        s_defaultProviders
            .Where(provider => provider.IsSupported(context))
            .GroupBy(provider => provider.Stage)
            .OrderBy(group => group.Key)
            .Select(group => (IReadOnlyList<InternalMicrosoftProbe>)group
                .Select(provider => new InternalMicrosoftProbe(
                    provider.Name,
                    cancellationToken => provider.DetectAsync(context, cancellationToken)))
                .ToArray())
            .ToArray();

    private async Task<InternalMicrosoftDetectionResult> RunProbeStagesAsync(
        string cacheStatus,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<InternalMicrosoftProbeDiagnostic>();
        var timedOut = false;

        foreach (var stage in _createProbeStages(_context))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stage.Count == 0)
            {
                continue;
            }

            var stageResult = await RunProbeStageAsync(stage, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(stageResult.Diagnostics);
            timedOut |= stageResult.TimedOut;
            if (stageResult.Result is { } detected)
            {
                return detected with
                {
                    IsCIEnvironment = IsCIEnvironment(),
                    CacheStatus = cacheStatus,
                    ProbeDiagnostics = diagnostics
                };
            }
        }

        var anyProbeFailed = diagnostics.Any(d => d.Outcome == InternalMicrosoftProbeOutcome.Failed);
        return new InternalMicrosoftDetectionResult(
            IsInternalMicrosoft: false,
            Source: null,
            Alias: null,
            Domain: null,
            IsCIEnvironment: IsCIEnvironment(),
            Outcome: timedOut
                ? InternalMicrosoftDetectorOutcome.TimedOut
                : anyProbeFailed
                    ? InternalMicrosoftDetectorOutcome.Failed
                    : InternalMicrosoftDetectorOutcome.NotDetected,
            CacheStatus: cacheStatus,
            Duration: TimeSpan.Zero,
            ProbeDiagnostics: diagnostics);
    }

    private async Task<InternalMicrosoftProbeStageResult> RunProbeStageAsync(
        IReadOnlyList<InternalMicrosoftProbe> probes,
        CancellationToken cancellationToken)
    {
        var stageStartTimestamp = Stopwatch.GetTimestamp();
        var stageDeadlineTimestamp = stageStartTimestamp + (long)(_probeStageTimeout.TotalSeconds * Stopwatch.Frequency);
        var stageTimeoutTimestamp = long.MaxValue;
        using var timeoutSource = _createProbeStageTimeoutSource(_probeStageTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        using var timeoutRegistration = timeoutSource.Token.Register(
            () => Interlocked.Exchange(ref stageTimeoutTimestamp, Stopwatch.GetTimestamp()));
        var tasks = probes.Select(probe => RunProbeAsync(probe, linkedSource.Token)).ToArray();
        var timedOut = false;

        try
        {
            await Task.WhenAll(tasks).WaitAsync(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            timedOut = true;
        }
        finally
        {
            await linkedSource.CancelAsync().ConfigureAwait(false);
            await DrainTasksAsync(tasks).ConfigureAwait(false);
        }

        stageDeadlineTimestamp = Math.Min(stageDeadlineTimestamp, Volatile.Read(ref stageTimeoutTimestamp));
        var completed = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();
        var diagnostics = completed
            .Select(result =>
            {
                var deadlineTriggeredCancellation =
                    timedOut && result.Diagnostic.Outcome == InternalMicrosoftProbeOutcome.Cancelled;
                return deadlineTriggeredCancellation || result.CompletionTimestamp > stageDeadlineTimestamp
                    ? result.Diagnostic with { Outcome = InternalMicrosoftProbeOutcome.TimedOut }
                    : result.Diagnostic;
            })
            .ToList();

        foreach (var probe in probes.Where(probe => !completed.Any(result => ReferenceEquals(result.Probe, probe))))
        {
            diagnostics.Add(new(probe.Name, InternalMicrosoftProbeOutcome.TimedOut, _probeStageTimeout, false, false));
        }

        timedOut |= completed.Any(result =>
            result.CompletionTimestamp > stageDeadlineTimestamp ||
            result.Diagnostic.Outcome == InternalMicrosoftProbeOutcome.TimedOut);

        var best = completed
            .Where(result => result.CompletionTimestamp <= stageDeadlineTimestamp && result.Result.IsInternalMicrosoft)
            .OrderByDescending(result => GetProbeResultScore(result.Result))
            .ThenBy(result => GetProbeIndex(probes, result.Probe))
            .FirstOrDefault();

        var detection = best is { Result.IsInternalMicrosoft: true }
            ? new InternalMicrosoftDetectionResult(
                true,
                best.Source,
                InternalMicrosoftDetectionUtilities.NormalizeAlias(best.Result.Alias),
                InternalMicrosoftDetectionUtilities.NormalizeDomain(best.Result.Domain),
                IsCIEnvironment(),
                InternalMicrosoftDetectorOutcome.Detected,
                InternalMicrosoftDetectorCacheStatus.Miss,
                TimeSpan.Zero,
                [])
            : null;

        return new(detection, diagnostics, timedOut);
    }

    private static Task<InternalMicrosoftProbeRunResult> RunProbeAsync(
        InternalMicrosoftProbe probe,
        CancellationToken cancellationToken) =>
        Task.Run<InternalMicrosoftProbeRunResult>(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await probe.DetectAsync(cancellationToken).ConfigureAwait(false);
                var outcome = result.Failure?.Code == InternalMicrosoftProbeFailureCode.ProcessTimeout
                    ? InternalMicrosoftProbeOutcome.TimedOut
                    : result.Failure is not null
                        ? InternalMicrosoftProbeOutcome.Failed
                        : result.IsInternalMicrosoft
                            ? InternalMicrosoftProbeOutcome.Detected
                            : InternalMicrosoftProbeOutcome.NotDetected;
                return new(
                    probe,
                    probe.Name,
                    result,
                    new(probe.Name, outcome, stopwatch.Elapsed, result.Alias is not null, result.Domain is not null, result.Failure),
                    Stopwatch.GetTimestamp());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new(
                    probe,
                    probe.Name,
                    InternalMicrosoftProbeResult.NotDetected,
                    new(probe.Name, InternalMicrosoftProbeOutcome.Cancelled, stopwatch.Elapsed, false, false),
                    Stopwatch.GetTimestamp());
            }
            catch (Exception exception)
            {
                var failure = InternalMicrosoftDetectionUtilities.CreateExceptionFailure(
                    exception,
                    InternalMicrosoftProbeFailureStage.Probe);
                return new(
                    probe,
                    probe.Name,
                    InternalMicrosoftProbeResult.Failed(failure),
                    new(probe.Name, InternalMicrosoftProbeOutcome.Failed, stopwatch.Elapsed, false, false, failure),
                    Stopwatch.GetTimestamp());
            }
        }, CancellationToken.None);

    private static int GetProbeResultScore(InternalMicrosoftProbeResult result) =>
        (result.Alias is null ? 0 : 2) + (result.Domain is null ? 0 : 1);

    private static int GetProbeIndex(IReadOnlyList<InternalMicrosoftProbe> probes, InternalMicrosoftProbe probe)
    {
        for (var index = 0; index < probes.Count; index++)
        {
            if (ReferenceEquals(probes[index], probe))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static async Task DrainTasksAsync(IReadOnlyList<Task<InternalMicrosoftProbeRunResult>> tasks)
    {
        try
        {
            await Task.WhenAll(tasks).WaitAsync(s_cancelledProbeDrainTimeout).ConfigureAwait(false);
        }
        catch
        {
            // Probe diagnostics already record bounded failure information.
        }
    }

    private async Task<(InternalMicrosoftDetectorCacheEntry? Entry, string CacheStatus)> TryReadCacheAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_cacheFilePath))
        {
            return (null, InternalMicrosoftDetectorCacheStatus.Miss);
        }

        try
        {
            await using var stream = new FileStream(
                _cacheFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);
            var entry = await JsonSerializer.DeserializeAsync(
                stream,
                InternalMicrosoftDetectorJsonContext.Default.InternalMicrosoftDetectorCacheEntry,
                cancellationToken).ConfigureAwait(false);
            var currentIsCI = IsCIEnvironment();
            if (entry is null ||
                entry.Version != CacheVersion ||
                entry.IsCIEnvironment != currentIsCI ||
                _timeProvider.GetUtcNow() - entry.TimestampUtc >= s_cacheRefreshInterval ||
                _timeProvider.GetUtcNow() < entry.TimestampUtc ||
                entry.IsInternalMicrosoft && string.IsNullOrEmpty(entry.Source))
            {
                return (null, InternalMicrosoftDetectorCacheStatus.Stale);
            }

            return (entry with
            {
                Alias = InternalMicrosoftDetectionUtilities.NormalizeAlias(entry.Alias),
                Domain = InternalMicrosoftDetectionUtilities.NormalizeDomain(entry.Domain)
            }, InternalMicrosoftDetectorCacheStatus.Hit);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return (null, InternalMicrosoftDetectorCacheStatus.Stale);
        }
    }

    private async Task TryWriteCacheAsync(
        InternalMicrosoftDetectionResult result,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_cacheFilePath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_cacheFilePath)}.{Guid.NewGuid():N}.tmp");
            var entry = new InternalMicrosoftDetectorCacheEntry(
                CacheVersion,
                result.IsInternalMicrosoft,
                result.Source,
                result.IsCIEnvironment ? null : result.Alias,
                result.IsCIEnvironment ? null : result.Domain,
                result.IsCIEnvironment,
                _timeProvider.GetUtcNow());
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    entry,
                    InternalMicrosoftDetectorJsonContext.Default.InternalMicrosoftDetectorCacheEntry,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _cacheFilePath, overwrite: true);
            temporaryPath = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A cache failure must not replace the classification result.
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Best-effort cleanup only.
                }
            }
        }
    }

    private bool IsCIEnvironment() => _context.IsCIEnvironment;
}
