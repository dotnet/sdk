// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.SdkCustomHelix.Sdk;

/// <summary>
/// A single Helix work item produced by the time-based scheduler.
/// Contains test methods (possibly from multiple assemblies) targeting a specific execution time.
/// </summary>
internal sealed class ScheduledWorkItem
{
    /// <summary>
    /// The test methods assigned to this work item.
    /// </summary>
    public List<TestMethodDiscovery.TestMethodInfo> TestMethods { get; } = new();

    /// <summary>
    /// Estimated total execution time based on historical data.
    /// </summary>
    public TimeSpan EstimatedDuration { get; private set; } = TimeSpan.Zero;

    /// <summary>
    /// Display name for this work item.
    /// </summary>
    public string DisplayName { get; set; } = "";

    public void AddTest(TestMethodDiscovery.TestMethodInfo method, TimeSpan duration)
    {
        TestMethods.Add(method);
        EstimatedDuration += duration;
    }

    /// <summary>
    /// Generates a --filter string for dotnet test / MTP at the method level.
    /// Uses FullyQualifiedName filter syntax compatible with both xUnit and MSTest.
    /// </summary>
    public string GetFilterString()
    {
        if (TestMethods.Count == 0)
            return string.Empty;

        // Use FullyQualifiedName~ (contains) filter for each method
        // Group by assembly to produce efficient filter expressions
        return string.Join("|", TestMethods.Select(m => m.FullyQualifiedName));
    }

    /// <summary>
    /// Gets the distinct assemblies represented in this work item.
    /// </summary>
    public IEnumerable<string> GetAssemblyPaths() =>
        TestMethods.Select(m => m.AssemblyPath).Distinct();
}

/// <summary>
/// Time-based test scheduler that uses historical execution durations from AzDO
/// to create Helix work items targeting a specific time budget per item.
/// Uses greedy first-fit bin-packing, processing tests in assembly order.
/// </summary>
internal sealed class TimeBasedScheduler
{
    /// <summary>
    /// Target execution time per work item. Default is 10 minutes.
    /// </summary>
    public static readonly TimeSpan DefaultWorkItemScheduleTime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Work item timeout is 3× the schedule time.
    /// </summary>
    public static readonly TimeSpan DefaultWorkItemTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Default number of work items when falling back to count-based scheduling.
    /// </summary>
    public const int DefaultFallbackWorkItemCount = 25;

    /// <summary>
    /// Maximum number of test methods per filter string before splitting.
    /// With the response file approach, this is no longer constrained by
    /// command-line length. We use a generous limit to avoid degenerate cases.
    /// </summary>
    private const int MaxFilterLength = 100000;

    private readonly TimeSpan _targetTime;
    private readonly int _fallbackWorkItemCount;

    public TimeBasedScheduler(TimeSpan? targetTime = null, int fallbackWorkItemCount = DefaultFallbackWorkItemCount, bool isPosixShell = true)
    {
        _targetTime = targetTime ?? DefaultWorkItemScheduleTime;
        _fallbackWorkItemCount = fallbackWorkItemCount;
    }

    /// <summary>
    /// Schedules test methods into work items using historical duration data.
    /// If history is null or empty, falls back to count-based partitioning.
    /// </summary>
    /// <param name="testMethods">All discovered test methods across assemblies.</param>
    /// <param name="history">Historical duration data keyed by fully-qualified test name. Null triggers fallback.</param>
    /// <returns>List of scheduled work items.</returns>
    public List<ScheduledWorkItem> Schedule(
        List<TestMethodDiscovery.TestMethodInfo> testMethods,
        Dictionary<string, TestExecutionInfo>? history)
    {
        if (testMethods.Count == 0)
            return new List<ScheduledWorkItem>();

        if (history is null || history.Count == 0)
            return ScheduleByCount(testMethods);

        return ScheduleByTime(testMethods, history);
    }

    /// <summary>
    /// Time-based scheduling using greedy first-fit bin-packing.
    /// </summary>
    private List<ScheduledWorkItem> ScheduleByTime(
        List<TestMethodDiscovery.TestMethodInfo> testMethods,
        Dictionary<string, TestExecutionInfo> history)
    {
        // Compute average duration for tests without history
        var knownDurations = history.Values.Select(v => v.Duration).Where(d => d > TimeSpan.Zero).ToList();
        var averageDuration = knownDurations.Count > 0
            ? TimeSpan.FromMilliseconds(knownDurations.Average(d => d.TotalMilliseconds))
            : TimeSpan.FromSeconds(5); // conservative default

        var workItems = new List<ScheduledWorkItem>();
        var currentItem = new ScheduledWorkItem();
        int currentFilterLength = 0;

        foreach (var method in testMethods)
        {
            // Look up historical duration, fall back to average
            var duration = history.TryGetValue(method.FullyQualifiedName, out var info) && info.Duration > TimeSpan.Zero
                ? info.Duration
                : averageDuration;

            // Check if adding this test would exceed limits
            int additionalFilterLength = method.FullyQualifiedName.Length + 1; // +1 for separator
            bool exceedsTime = currentItem.TestMethods.Count > 0 &&
                               currentItem.EstimatedDuration + duration > _targetTime;
            bool exceedsLength = currentFilterLength + additionalFilterLength > MaxFilterLength;

            if (currentItem.TestMethods.Count > 0 && (exceedsTime || exceedsLength))
            {
                // Flush current work item
                FinalizeWorkItem(workItems, currentItem);
                currentItem = new ScheduledWorkItem();
                currentFilterLength = 0;
            }

            currentItem.AddTest(method, duration);
            currentFilterLength += additionalFilterLength;

            // Special case: single test exceeds target time — give it a dedicated work item
            if (currentItem.TestMethods.Count == 1 && duration > _targetTime)
            {
                FinalizeWorkItem(workItems, currentItem);
                currentItem = new ScheduledWorkItem();
                currentFilterLength = 0;
            }
        }

        // Flush remaining
        if (currentItem.TestMethods.Count > 0)
        {
            FinalizeWorkItem(workItems, currentItem);
        }

        return workItems;
    }

    /// <summary>
    /// Count-based fallback: distributes tests evenly across N work items,
    /// while also respecting the command-line filter length limit.
    /// </summary>
    private List<ScheduledWorkItem> ScheduleByCount(List<TestMethodDiscovery.TestMethodInfo> testMethods)
    {
        var workItemCount = Math.Min(_fallbackWorkItemCount, testMethods.Count);
        var testsPerItem = (int)Math.Ceiling((double)testMethods.Count / workItemCount);

        var workItems = new List<ScheduledWorkItem>();
        var currentItem = new ScheduledWorkItem();
        var defaultDuration = TimeSpan.FromSeconds(5);
        int currentFilterLength = 0;

        foreach (var method in testMethods)
        {
            int additionalFilterLength = method.FullyQualifiedName.Length + 1;

            if (currentItem.TestMethods.Count >= testsPerItem ||
                currentFilterLength + additionalFilterLength > MaxFilterLength)
            {
                FinalizeWorkItem(workItems, currentItem);
                currentItem = new ScheduledWorkItem();
                currentFilterLength = 0;
            }

            currentItem.AddTest(method, defaultDuration);
            currentFilterLength += additionalFilterLength;
        }

        if (currentItem.TestMethods.Count > 0)
        {
            FinalizeWorkItem(workItems, currentItem);
        }

        return workItems;
    }

    private static void FinalizeWorkItem(List<ScheduledWorkItem> workItems, ScheduledWorkItem item)
    {
        var index = workItems.Count + 1;
        var assemblies = item.GetAssemblyPaths().Select(Path.GetFileNameWithoutExtension);
        var assemblyLabel = string.Join("+", assemblies.Take(3));
        if (item.GetAssemblyPaths().Count() > 3)
            assemblyLabel += "+...";

        item.DisplayName = $"{assemblyLabel}.Part{index}";
        workItems.Add(item);
    }
}
