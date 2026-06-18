// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.SdkCustomHelix.Sdk;

/// <summary>
/// Local validation tool for the time-based Helix test scheduler.
/// Usage:
///   dotnet run -- &lt;assembly-path&gt; [--azdo-uri &lt;uri&gt;] [--azdo-token &lt;token&gt;]
///                [--definition-id &lt;id&gt;] [--branch &lt;branch&gt;] [--target-minutes &lt;min&gt;]
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        var assemblyPaths = new List<string>();
        string? azdoUri = null;
        string? azdoToken = null;
        int definitionId = 0;
        string branch = "main";
        int targetMinutes = 10;
        string? phaseName = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--azdo-uri" when i + 1 < args.Length:
                    azdoUri = args[++i];
                    break;
                case "--azdo-token" when i + 1 < args.Length:
                    azdoToken = args[++i];
                    break;
                case "--definition-id" when i + 1 < args.Length:
                    definitionId = int.Parse(args[++i]);
                    break;
                case "--branch" when i + 1 < args.Length:
                    branch = args[++i];
                    break;
                case "--target-minutes" when i + 1 < args.Length:
                    targetMinutes = int.Parse(args[++i]);
                    break;
                case "--phase" when i + 1 < args.Length:
                    phaseName = args[++i];
                    break;
                default:
                    if (!args[i].StartsWith("--"))
                        assemblyPaths.Add(args[i]);
                    break;
            }
        }

        if (assemblyPaths.Count == 0)
        {
            Console.Error.WriteLine("Error: At least one assembly path is required.");
            return 1;
        }

        // Discover test methods
        Console.WriteLine("=== Test Method Discovery ===");
        var allMethods = new List<TestMethodDiscovery.TestMethodInfo>();
        foreach (var path in assemblyPaths)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"  Assembly not found: {path}");
                continue;
            }

            var methods = TestMethodDiscovery.DiscoverTestMethods(path);
            Console.WriteLine($"  {Path.GetFileName(path)}: {methods.Count} test methods");
            allMethods.AddRange(methods);
        }

        Console.WriteLine($"  Total: {allMethods.Count} test methods");
        Console.WriteLine();

        // Fetch test history if AzDO credentials provided
        Dictionary<string, TestExecutionInfo>? history = null;
        if (!string.IsNullOrEmpty(azdoUri) && !string.IsNullOrEmpty(azdoToken) && definitionId > 0)
        {
            Console.WriteLine("=== Fetching AzDO Test History ===");
            var historyManager = new TestHistoryManager(azdoUri, azdoToken, definitionId, branch, phaseName);
            history = await historyManager.GetTestHistoryAsync();

            if (history is not null)
            {
                Console.WriteLine($"  Retrieved history for {history.Count} test methods");
                var matched = allMethods.Count(m => history.ContainsKey(m.FullyQualifiedName));
                Console.WriteLine($"  Matched {matched}/{allMethods.Count} discovered methods ({100.0 * matched / allMethods.Count:F1}%)");
            }
            else
            {
                Console.WriteLine("  No history available (will use count-based fallback)");
            }
            Console.WriteLine();
        }

        // Run scheduler
        Console.WriteLine($"=== Scheduling (target: {targetMinutes} min/work item) ===");
        var scheduler = new TimeBasedScheduler(TimeSpan.FromMinutes(targetMinutes));
        var workItems = scheduler.Schedule(allMethods, history);

        Console.WriteLine($"  Produced {workItems.Count} work items");
        Console.WriteLine();

        // Print work item plan
        Console.WriteLine("=== Work Item Plan ===");
        Console.WriteLine($"{"#",-4} {"Name",-40} {"Tests",-8} {"Est. Duration",-15} {"Assemblies"}");
        Console.WriteLine(new string('-', 100));

        var totalEstimated = TimeSpan.Zero;
        for (int i = 0; i < workItems.Count; i++)
        {
            var wi = workItems[i];
            totalEstimated += wi.EstimatedDuration;
            var assemblies = string.Join(", ", wi.GetAssemblyPaths().Select(Path.GetFileNameWithoutExtension));
            Console.WriteLine($"{i + 1,-4} {wi.DisplayName,-40} {wi.TestMethods.Count,-8} {wi.EstimatedDuration:hh\\:mm\\:ss\\.fff,-15} {assemblies}");
        }

        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"     {"TOTAL",-40} {allMethods.Count,-8} {totalEstimated:hh\\:mm\\:ss\\.fff}");
        Console.WriteLine();

        if (history is not null)
        {
            // Show distribution stats
            var durations = workItems.Select(w => w.EstimatedDuration.TotalMinutes).ToList();
            Console.WriteLine("=== Distribution Stats ===");
            Console.WriteLine($"  Min work item: {durations.Min():F2} min");
            Console.WriteLine($"  Max work item: {durations.Max():F2} min");
            Console.WriteLine($"  Avg work item: {durations.Average():F2} min");
            Console.WriteLine($"  Std deviation: {StdDev(durations):F2} min");
        }

        return 0;
    }

    static double StdDev(List<double> values)
    {
        var avg = values.Average();
        var sumSquares = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSquares / values.Count);
    }

    static void PrintUsage()
    {
        Console.WriteLine("""
            Helix Time-Based Scheduler - Local Validation Tool

            Usage:
              dotnet run -- <assembly-path> [<assembly-path>...] [options]

            Options:
              --azdo-uri <uri>          AzDO project URI (e.g. https://dev.azure.com/dnceng/public)
              --azdo-token <token>      AzDO access token
              --definition-id <id>      Pipeline definition ID
              --branch <branch>         Target branch (default: main)
              --phase <name>            Phase/stage name to filter test runs
              --target-minutes <min>    Target minutes per work item (default: 10)
              -h, --help                Show this help

            Examples:
              # Local-only (count-based fallback, no AzDO):
              dotnet run -- path/to/tests.dll

              # With AzDO history:
              dotnet run -- path/to/tests.dll --azdo-uri https://dev.azure.com/dnceng/public \
                --azdo-token $TOKEN --definition-id 1234 --branch main
            """);
    }
}
