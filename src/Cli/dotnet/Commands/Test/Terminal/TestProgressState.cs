// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, int LastAttemptNumber);

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch)
{
    private readonly Dictionary<string, TestNodeInfoEntry> _testUidToResults = new();

    // In most cases, retries don't happen. So we start with a capacity of 1.
    // Resizes will be rare and will be okay with such small sizes.
    private readonly List<string> _orderedInstanceIds = new(capacity: 1);

    public string Assembly { get; } = assembly;

    public string AssemblyName { get; } = Path.GetFileName(assembly)!;

    public string? TargetFramework { get; } = targetFramework;

    public string? Architecture { get; } = architecture;

    public IStopwatch Stopwatch { get; } = stopwatch;

    public int FailedTests { get; private set; }

    public int PassedTests { get; private set; }

    public int SkippedTests { get; private set; }

    public int TotalTests => PassedTests + SkippedTests + FailedTests;

    public int RetriedFailedTests { get; private set; }

    public TestNodeResultsState? TestNodeResultsState { get; internal set; }

    public int SlotIndex { get; internal set; }

    public long Id { get; internal set; } = id;

    public long Version { get; internal set; }

    public List<(string? DisplayName, string? UID)> DiscoveredTests { get; internal set; } = [];

    public bool Success { get; internal set; }

    public int TryCount { get; private set; }

    private void ReportGenericTestResult(
        string testNodeUid,
        string instanceId,
        Func<TestNodeInfoEntry, TestNodeInfoEntry> incrementTestNodeInfoEntry,
        Action<TestProgressState> incrementCountAction)
    {
        var currentAttemptNumber = GetAttemptNumberFromInstanceId(instanceId);

        if (_testUidToResults.TryGetValue(testNodeUid, out var value))
        {
            // We received a result for this test node uid before.
            if (value.LastAttemptNumber == currentAttemptNumber)
            {
                // We are getting a test result for the same attempt.
                // This means that the test framework is reporting multiple results for the same test node uid.
                // We will just increment the count of the result.
                _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry(value);
            }
            else if (currentAttemptNumber > value.LastAttemptNumber)
            {
                // This is a retry!
                // We are getting a test result for a different instance id.
                // This means that the test was retried.
                // We discard the results from the previous instance id
                RetriedFailedTests += value.Failed;
                PassedTests -= value.Passed;
                SkippedTests -= value.Skipped;
                FailedTests -= value.Failed;
                _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber));
            }
            else
            {
                // This is an unexpected case where we received a result for an instance id that is older than the last one we saw.
                throw new UnreachableException($"Unexpected test result for attempt '{currentAttemptNumber}' while the last attempt is '{value.LastAttemptNumber}'");
            }
        }
        else
        {
            // This is the first time we see this test node.
            _testUidToResults.Add(testNodeUid, incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastAttemptNumber: currentAttemptNumber)));
        }

        incrementCountAction(this);
    }

    public void ReportPassingTest(string testNodeUid, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, instanceId, static entry =>
        {
            entry.Passed++;
            return entry;
        }, static @this => @this.PassedTests++);
    }

    public void ReportSkippedTest(string testNodeUid, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, instanceId, static entry =>
        {
            entry.Skipped++;
            return entry;
        }, static @this => @this.SkippedTests++);
    }

    public void ReportFailedTest(string testNodeUid, string instanceId)
    {
        ReportGenericTestResult(testNodeUid, instanceId, static entry =>
        {
            entry.Failed++;
            return entry;
        }, static @this => @this.FailedTests++);
    }

    public void DiscoverTest(string? displayName, string? uid)
    {
        PassedTests++;
        DiscoveredTests.Add(new(displayName, uid));
    }

    internal void NotifyHandshake(string instanceId)
    {
        var index = _orderedInstanceIds.IndexOf(instanceId);
        if (index < 0)
        {
            // New instanceId for a retry. We add it to _orderedInstanceIds.
            _orderedInstanceIds.Add(instanceId);
            TryCount++;
        }
        else if (index != _orderedInstanceIds.Count - 1)
        {
            // This is an unexpected case where we received a handshake for an instance id that is not the last one we saw.
            // This means that the test framework is trying to report results for an instance id that is not the last one.
            throw new UnreachableException($"Unexpected handshake for instance id '{instanceId}' at index '{index}' while the last index is '{_orderedInstanceIds.Count - 1}'");
        }
    }

    private int GetAttemptNumberFromInstanceId(string instanceId)
    {
        var index = _orderedInstanceIds.IndexOf(instanceId);
        if (index < 0)
        {
            throw new UnreachableException($"The instanceId '{instanceId}' not found.");
        }

        // Attempt numbers are 1-based, so we add 1 to the index.
        return index + 1;
    }
}
