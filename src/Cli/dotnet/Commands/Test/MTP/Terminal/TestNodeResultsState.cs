// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Globalization;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TestNodeResultsState(long id)
{
    public long Id { get; } = id;

    private readonly TestDetailState _summaryDetail = new(id, stopwatch: null, text: string.Empty);
    private readonly ConcurrentDictionary<string, TestDetailState> _testNodeProgressStates = new();
    private readonly ConcurrentDictionary<string, byte> _completed = new();

    public int Count => _testNodeProgressStates.Count;

    public void AddRunningTestNode(int id, string instanceId, string uid, string name, IStopwatch stopwatch)
    {
        string key = MakeKey(instanceId, uid);

        // Guard against stale "in-progress" notifications that arrive after the
        // test already completed. Without this we could surface a "running"
        // entry that will never be removed.
        if (_completed.ContainsKey(key))
        {
            return;
        }

        _testNodeProgressStates[key] = new TestDetailState(id, stopwatch, name);
    }

    public void RemoveRunningTestNode(string instanceId, string uid)
    {
        string key = MakeKey(instanceId, uid);
        _completed[key] = 0;
        _testNodeProgressStates.TryRemove(key, out _);
    }

    private static string MakeKey(string instanceId, string uid) => $"{instanceId}\u0000{uid}";

    public IEnumerable<TestDetailState> GetRunningTasks(int maxCount)
    {
        var sortedDetails = _testNodeProgressStates
            .Select(d => d.Value)
            .OrderByDescending(d => d.Stopwatch?.Elapsed ?? TimeSpan.Zero)
            .ToList();

        bool tooManyItems = sortedDetails.Count > maxCount;

        if (tooManyItems)
        {
            // Note: If there's too many items to display, the summary will take up one line.
            // As such, we can only take maxCount - 1 items.
            int itemsToTake = maxCount - 1;
            _summaryDetail.Text =
                itemsToTake == 0
                    // Note: If itemsToTake is 0, then we only show two lines, the project summary and the number of running tests.
                    ? string.Format(CultureInfo.CurrentCulture, CliCommandStrings.ActiveTestsRunning_FullTestsCount, sortedDetails.Count)
                    // If itemsToTake is larger, then we show the project summary, active tests, and the number of active tests that are not shown.
                    : $"... {string.Format(CultureInfo.CurrentCulture, CliCommandStrings.ActiveTestsRunning_MoreTestsCount, sortedDetails.Count - itemsToTake)}";
            sortedDetails = [.. sortedDetails.Take(itemsToTake)];
        }

        foreach (TestDetailState? detail in sortedDetails)
        {
            yield return detail;
        }

        if (tooManyItems)
        {
            yield return _summaryDetail;
        }
    }
}
