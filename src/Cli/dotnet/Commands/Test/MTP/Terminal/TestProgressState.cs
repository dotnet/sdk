// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber, int Attempts);

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch, bool isDiscovery)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, TestNodeInfoEntry> _testUidToResults = new();
    private readonly Dictionary<string, int> _instanceIdToAttemptNumber = new();

    /// <summary>
    /// Records the last-seen (display name, duration) for every test node, keyed by test node uid, so the
    /// "slowest tests" summary section can rank them. Keyed by uid (not appended to a list) so a retried test
    /// replaces its earlier attempt's timing instead of appearing twice, mirroring the pass/fail tally above.
    /// Only populated when the slowest-tests feature is enabled (the reporter gates the RecordTestDuration call),
    /// so a run without the feature pays no memory cost here.
    /// </summary>
    private readonly Dictionary<string, (string DisplayName, TimeSpan Duration)> _testUidToDuration = new();

    /// <summary>
    /// Test nodes whose result was superseded by a later attempt while the earlier attempt had at least one failure.
    /// Combined with the final tally in <see cref="_testUidToResults"/> this yields the "flaky" set (failed at least
    /// once, but the final attempt passed). Kept separate from the tally so a test that keeps failing is
    /// retried-but-not-flaky. The value is the last-seen display name so the summary can list the test by name.
    /// </summary>
    private readonly Dictionary<string, string> _uidWithEarlierFailure = new();

    /// <summary>
    /// Distinct test nodes that produced results in more than one attempt. This is the "how many tests were retried"
    /// figure, as opposed to <see cref="_retriedExecutions"/> ("how many extra runs did that cost").
    /// </summary>
    private readonly HashSet<string> _retriedUids = new(StringComparer.Ordinal);

    private readonly List<DiscoveredTestInfo> _discoveredTestNames = [];
    private int _discoveredTests;
    private int _failedTests;
    private int _passedTests;
    private int _skippedTests;
    private int _retriedFailedTests;

    /// <summary>
    /// Total number of extra executions caused by retries (every result that superseded an earlier attempt), so the
    /// summary can distinguish "2 tests were retried" from "those retries cost 4 extra runs".
    /// </summary>
    private int _retriedExecutions;
    private int _tryCount;
    private TestNodeResultsState? _testNodeResultsState;
    private bool _success;

    public string Assembly { get; } = assembly;

    public string AssemblyName { get; } = Path.GetFileName(assembly)!;

    public string? TargetFramework { get; } = targetFramework;

    public string? Architecture { get; } = architecture;

    public IStopwatch Stopwatch { get; } = stopwatch;

    public int DiscoveredTests
    {
        get
        {
            lock (_lock)
            {
                return _discoveredTests;
            }
        }
    }

    public int FailedTests
    {
        get
        {
            lock (_lock)
            {
                return _failedTests;
            }
        }
    }

    public int PassedTests
    {
        get
        {
            lock (_lock)
            {
                return _passedTests;
            }
        }
    }

    public int SkippedTests
    {
        get
        {
            lock (_lock)
            {
                return _skippedTests;
            }
        }
    }

    public int TotalTests
    {
        get
        {
            lock (_lock)
            {
                return IsDiscovery ? _discoveredTests : _passedTests + _skippedTests + _failedTests;
            }
        }
    }

    public int RetriedFailedTests
    {
        get
        {
            lock (_lock)
            {
                return _retriedFailedTests;
            }
        }
    }

    /// <summary>
    /// Gets the number of distinct tests that produced results in more than one attempt.
    /// </summary>
    public int RetriedTests
    {
        get
        {
            lock (_lock)
            {
                return _retriedUids.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of extra executions caused by retries (results that superseded an earlier attempt).
    /// </summary>
    public int RetriedExecutions
    {
        get
        {
            lock (_lock)
            {
                return _retriedExecutions;
            }
        }
    }

    /// <summary>
    /// Gets the number of tests that failed at least once but whose final attempt passed.
    /// </summary>
    public int FlakyTests
    {
        get
        {
            lock (_lock)
            {
                int count = 0;
                foreach (KeyValuePair<string, string> entry in _uidWithEarlierFailure)
                {
                    if (IsFlakyCore(entry.Key))
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    public TestNodeResultsState? TestNodeResultsState
    {
        get
        {
            lock (_lock)
            {
                return _testNodeResultsState;
            }
        }
    }

    public int SlotIndex { get; internal set; }

    public long Id { get; internal set; } = id;

    public long Version { get; internal set; }

    public List<DiscoveredTestInfo> DiscoveredTestNames
    {
        get
        {
            lock (_lock)
            {
                return [.. _discoveredTestNames];
            }
        }
    }

    public bool Success
    {
        get
        {
            lock (_lock)
            {
                return _success;
            }
        }

        internal set
        {
            lock (_lock)
            {
                _success = value;
            }
        }
    }

    public int? ExitCode { get; internal set; }

    public bool IsDiscovery { get; } = isDiscovery;

    public int TryCount
    {
        get
        {
            lock (_lock)
            {
                return _tryCount;
            }
        }
    }

    private void ReportGenericTestResult(
        string testNodeUid,
        string displayName,
        string instanceId,
        Func<TestNodeInfoEntry, TestNodeInfoEntry> incrementTestNodeInfoEntry,
        Action<TestProgressState> incrementCountAction)
    {
        lock (_lock)
        {
            int currentAttemptNumber = GetAttemptNumberCore(instanceId);

            if (_testUidToResults.TryGetValue(testNodeUid, out var value))
            {
                if (value.LastAttemptNumber == currentAttemptNumber)
                {
                    // Another result for the same test node in the same attempt — just increment. When the uid has
                    // already been superseded once, this result belongs to a retry attempt and is itself an extra
                    // execution: a folded data-driven test reports one result per row, so counting only the first
                    // would undercount the extra runs those rows actually cost.
                    if (_retriedUids.Contains(testNodeUid))
                    {
                        _retriedExecutions++;
                    }

                    _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry(value);
                }
                else if (currentAttemptNumber > value.LastAttemptNumber)
                {
                    _retriedFailedTests += value.Failed;
                    _passedTests -= value.Passed;
                    _skippedTests -= value.Skipped;
                    _failedTests -= value.Failed;
                    _retriedUids.Add(testNodeUid);
                    _retriedExecutions++;

                    // Remember that an earlier attempt failed. Whether that makes the test flaky depends on the
                    // final tally, which is only known once the run ends, so the decision is deferred to
                    // IsFlakyCore rather than made here.
                    if (value.Failed > 0)
                    {
                        _uidWithEarlierFailure[testNodeUid] = displayName;
                    }

                    _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber, Attempts: value.Attempts + 1));
                }
                else
                {
                    throw new UnreachableException($"Unexpected test result for attempt '{currentAttemptNumber}' while the last attempt is '{value.LastAttemptNumber}'");
                }
            }
            else
            {
                _testUidToResults.Add(testNodeUid, incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber, Attempts: 1)));
            }

            incrementCountAction(this);
        }
    }

    public void ReportPassingTest(string testNodeUid, string displayName, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, displayName, instanceId, static entry =>
        {
            entry.Passed++;
            return entry;
        }, static @this => @this._passedTests++);
    }

    public void ReportSkippedTest(string testNodeUid, string displayName, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, displayName, instanceId, static entry =>
        {
            entry.Skipped++;
            return entry;
        }, static @this => @this._skippedTests++);
    }

    public void ReportFailedTest(string testNodeUid, string displayName, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, displayName, instanceId, static entry =>
        {
            entry.Failed++;
            return entry;
        }, static @this => @this._failedTests++);
    }

    /// <summary>
    /// Records (or clears) the last-seen duration reported for a test node so it can be ranked in the "slowest
    /// tests" summary section. Keyed by <paramref name="testNodeUid"/> so a retry (which re-reports the same uid)
    /// replaces the earlier attempt's timing rather than adding a duplicate entry. A <see langword="null"/>
    /// <paramref name="duration"/> means the latest attempt reported no timing, so the earlier attempt's stale
    /// duration is removed rather than kept. Only invoked when the slowest-tests feature is enabled.
    /// </summary>
    public void RecordTestDuration(string testNodeUid, string displayName, TimeSpan? duration)
    {
        lock (_lock)
        {
            if (duration.HasValue)
            {
                _testUidToDuration[testNodeUid] = (displayName, duration.Value);
            }
            else
            {
                _testUidToDuration.Remove(testNodeUid);
            }
        }
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> recorded tests ordered from slowest to fastest. Ties are broken by
    /// display name (ordinal) so the ranking is deterministic for snapshot-based tests.
    /// </summary>
    public IReadOnlyList<(string DisplayName, TimeSpan Duration)> GetSlowestTests(int count)
    {
        lock (_lock)
        {
            return count <= 0 || _testUidToDuration.Count == 0
                ? []
                : [.. _testUidToDuration.Values
                    .OrderByDescending(static entry => entry.Duration)
                    .ThenBy(static entry => entry.DisplayName, StringComparer.Ordinal)
                    .Take(count)];
        }
    }

    /// <summary>
    /// Returns the tests that failed at least once but eventually passed, as (display name, total attempts) pairs
    /// ordered by display name so the rendering is deterministic for snapshot-based tests.
    /// </summary>
    public IReadOnlyList<(string DisplayName, int Attempts)> GetFlakyTests()
    {
        lock (_lock)
        {
            if (_uidWithEarlierFailure.Count == 0)
            {
                return [];
            }

            List<(string DisplayName, int Attempts)> flaky = [];
            foreach (KeyValuePair<string, string> entry in _uidWithEarlierFailure)
            {
                if (IsFlakyCore(entry.Key))
                {
                    flaky.Add((entry.Value, _testUidToResults[entry.Key].Attempts));
                }
            }

            flaky.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName));
            return flaky;
        }
    }

    /// <summary>
    /// A test is flaky when an earlier attempt failed but the final attempt produced only passing results. Callers
    /// must hold <see cref="_lock"/>.
    /// </summary>
    private bool IsFlakyCore(string testNodeUid)
        // A skipped row under a folded uid is not recovery either: not every result of the final attempt passed.
        => _testUidToResults.TryGetValue(testNodeUid, out TestNodeInfoEntry entry)
            && entry.Failed == 0
            && entry.Skipped == 0
            && entry.Passed > 0;

    public void DiscoverTest(DiscoveredTestInfo test)
    {
        lock (_lock)
        {
            _discoveredTests++;
            _discoveredTestNames.Add(test);
        }
    }

    internal void NotifyHandshake(string instanceId)
        => NotifyHandshakeCore(instanceId, attemptNumber: null);

    internal void NotifyHandshake(string instanceId, int attemptNumber)
        => NotifyHandshakeCore(instanceId, attemptNumber);

    private void NotifyHandshakeCore(string instanceId, int? attemptNumber)
    {
        lock (_lock)
        {
            if (_instanceIdToAttemptNumber.TryGetValue(instanceId, out int registeredAttemptNumber))
            {
                if (attemptNumber.HasValue && attemptNumber.Value != registeredAttemptNumber)
                {
                    throw new UnreachableException($"Instance id '{instanceId}' was already registered for attempt '{registeredAttemptNumber}', not '{attemptNumber.Value}'.");
                }

                return;
            }

            int resolvedAttemptNumber = attemptNumber ?? _tryCount + 1;
            if (resolvedAttemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber));
            }

            _instanceIdToAttemptNumber.Add(instanceId, resolvedAttemptNumber);
            _tryCount = Math.Max(_tryCount, resolvedAttemptNumber);
        }
    }

    internal int GetAttemptNumber(string instanceId)
    {
        lock (_lock)
        {
            return GetAttemptNumberCore(instanceId);
        }
    }

    internal TestNodeResultsState GetOrCreateTestNodeResultsState(Func<TestNodeResultsState> factory)
    {
        lock (_lock)
        {
            return _testNodeResultsState ??= factory();
        }
    }

    private int GetAttemptNumberCore(string instanceId)
        => _instanceIdToAttemptNumber.TryGetValue(instanceId, out int attemptNumber)
            ? attemptNumber
            : throw new UnreachableException($"The instanceId '{instanceId}' not found.");
}

/// <summary>
/// Rich information about a single discovered test node, as received over the 'dotnet test' IPC
/// protocol (<c>DiscoveredTestMessage</c>). Carries every field the wire contract provides so the
/// SDK can render both the human-readable and the machine-readable ('--list-tests json') output.
/// </summary>
internal sealed record DiscoveredTestInfo(
    string? DisplayName,
    string? Uid,
    string? FilePath,
    int? LineNumber,
    string? Namespace,
    string? TypeName,
    string? MethodName,
    string[] ParameterTypeFullNames,
    (string Key, string Value)[] Traits);
