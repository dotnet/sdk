// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestNodeInfoEntry = (int Passed, int Skipped, int Failed, string LastInstanceId);

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch)
{
    private readonly Dictionary<string, TestNodeInfoEntry> _testUidToResults = new();
    private readonly HashSet<string> _seenInstanceIds = new();
    private string? _lastReceivedInstanceId;

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

    public int TryCount { get; internal set; }

    private void ReportGenericTestResult(
        string testNodeUid,
        string instanceId,
        Func<TestNodeInfoEntry, TestNodeInfoEntry> incrementTestNodeInfoEntry,
        Action<TestProgressState> incrementCountAction)
    {
        if (_lastReceivedInstanceId is not null && _lastReceivedInstanceId != instanceId &&
            _seenInstanceIds.Contains(instanceId))
        {
            // This instanceId was seen before, but it's not the last one we received!
            // This is unexpected to happen.
            // It means that we received a 3 test results with the same execution id.
            // First had instance id X.
            // Then second had instance id Y.
            // Then third had instance id X again.
            // Let's be safe and throw an exception instead of potentially showing wrong test outcome!
            // If this happened in practice, we need to know about it.
            throw new InvalidOperationException("This program location is thought to be unreachable.");
        }

        _lastReceivedInstanceId = instanceId;

        if (_testUidToResults.TryGetValue(testNodeUid, out var value))
        {
            // We received a result for this test node uid before.
            if (value.LastInstanceId == instanceId)
            {
                // We are getting a test result for the same instance id.
                // This means that the test framework is reporting multiple results for the same test node uid.
                // We will just increment the passed count.
                _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry(value);
            }
            else if (_seenInstanceIds.Contains(instanceId))
            {
                throw new InvalidOperationException("This program location is thought to be unreachable.");
            }
            else
            {
                // We are getting a test result for a different instance id.
                // This means that the test was retried.
                // We discard the results from the previous instance id
                RetriedFailedTests++;
                PassedTests -= value.Passed;
                SkippedTests -= value.Skipped;
                FailedTests -= value.Failed;
                _testUidToResults[testNodeUid] = incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastInstanceId: instanceId));
            }
        }
        else
        {
            // This is the first time we see this test node.
            _testUidToResults.Add(testNodeUid, incrementTestNodeInfoEntry((Passed: 0, Skipped: 0, Failed: 0, LastInstanceId: instanceId)));
        }

        _seenInstanceIds.Add(instanceId);
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
}
