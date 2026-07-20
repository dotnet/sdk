// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber);

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch, bool isDiscovery)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, TestNodeInfoEntry> _testUidToResults = new();
    private readonly Dictionary<string, int> _instanceIdToAttemptNumber = new();
    private readonly List<(string? DisplayName, string? UID, string? FilePath, int? LineNumber)> _discoveredTestNames = [];
    private int _discoveredTests;
    private int _failedTests;
    private int _passedTests;
    private int _skippedTests;
    private int _retriedFailedTests;
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

    public List<(string? DisplayName, string? UID, string? FilePath, int? LineNumber)> DiscoveredTestNames
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
                    _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry(value);
                }
                else if (currentAttemptNumber > value.LastAttemptNumber)
                {
                    _retriedFailedTests += value.Failed;
                    _passedTests -= value.Passed;
                    _skippedTests -= value.Skipped;
                    _failedTests -= value.Failed;
                    _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber));
                }
                else
                {
                    throw new UnreachableException($"Unexpected test result for attempt '{currentAttemptNumber}' while the last attempt is '{value.LastAttemptNumber}'");
                }
            }
            else
            {
                _testUidToResults.Add(testNodeUid, incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber)));
            }

            incrementCountAction(this);
        }
    }

    public void ReportPassingTest(string testNodeUid, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, instanceId, static entry =>
        {
            entry.Passed++;
            return entry;
        }, static @this => @this._passedTests++);
    }

    public void ReportSkippedTest(string testNodeUid, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, instanceId, static entry =>
        {
            entry.Skipped++;
            return entry;
        }, static @this => @this._skippedTests++);
    }

    public void ReportFailedTest(string testNodeUid, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, instanceId, static entry =>
        {
            entry.Failed++;
            return entry;
        }, static @this => @this._failedTests++);
    }

    public void DiscoverTest(string? displayName, string? uid, string? filePath, int? lineNumber)
    {
        lock (_lock)
        {
            _discoveredTests++;
            _discoveredTestNames.Add(new(displayName, uid, filePath, lineNumber));
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
