// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch)
{
    private readonly Dictionary<string, (int Passed, int Skipped, int Failed, string LastInstanceId)> _testUidToResults = new();

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

    public int? ExitCode { get; internal set; }

    public bool Success { get; internal set; }

    public int TryCount { get; internal set; }

    public HashSet<string> FlakyTests { get; } = [];

    public void ReportPassingTest(string testNodeUid, string instanceId)
    {
        if (_testUidToResults.TryGetValue(testNodeUid, out var value))
        {
            if (value.LastInstanceId == instanceId)
            {
                // We are getting a test result for the same instance id.
                value.Passed++;
                _testUidToResults[testNodeUid] = value;
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
                _testUidToResults[testNodeUid] = (Passed: 1, Skipped: 0, Failed: 0, LastInstanceId: instanceId);
            }
        }
        else
        {
            // This is the first time we see this test node.
            _testUidToResults.Add(testNodeUid, (Passed: 1, Skipped: 0, Failed: 0, LastInstanceId: instanceId));
        }

        PassedTests++;
    }

    public void ReportSkippedTest(string testNodeUid, string instanceId)
    {
        if (_testUidToResults.TryGetValue(testNodeUid, out var value))
        {
            if (value.LastInstanceId == instanceId)
            {
                value.Skipped++;
                _testUidToResults[testNodeUid] = value;
            }
            else
            {
                PassedTests -= value.Passed;
                SkippedTests -= value.Skipped;
                FailedTests -= value.Failed;
                _testUidToResults[testNodeUid] = (Passed: 0, Skipped: 1, Failed: 0, LastInstanceId: instanceId);
            }
        }
        else
        {
            _testUidToResults.Add(testNodeUid, (Passed: 0, Skipped: 1, Failed: 0, LastInstanceId: instanceId));
        }

        SkippedTests++;
    }

    public void ReportFailedTest(string testNodeUid, string instanceId)
    {
        if (_testUidToResults.TryGetValue(testNodeUid, out var value))
        {
            if (value.LastInstanceId == instanceId)
            {
                value.Failed++;
                _testUidToResults[testNodeUid] = value;
            }
            else
            {
                PassedTests -= value.Passed;
                SkippedTests -= value.Skipped;
                FailedTests -= value.Failed;
                _testUidToResults[testNodeUid] = (Passed: 0, Skipped: 0, Failed: 1, LastInstanceId: instanceId);
            }
        }
        else
        {
            _testUidToResults.Add(testNodeUid, (Passed: 0, Skipped: 0, Failed: 1, LastInstanceId: instanceId));
        }

        FailedTests++;
    }

    public void DiscoverTest(string? displayName, string? uid)
    {
        PassedTests++;
        DiscoveredTests.Add(new(displayName, uid));
    }
}
