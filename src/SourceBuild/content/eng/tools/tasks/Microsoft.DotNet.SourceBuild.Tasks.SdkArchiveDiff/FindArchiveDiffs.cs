// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = System.Threading.Tasks.Task;

public class FindArchiveDiffs : Microsoft.Build.Utilities.Task
{
    public class ArchiveItem
    {
        public required string Path { get; init; }
    }

    [Required]
    public required ITaskItem BaselineArchive { get; init; }

    [Required]
    public required ITaskItem TestArchive { get; init; }

    [Output]
    public ITaskItem[] ContentDifferences { get; set; } = [];

    public override bool Execute()
    {
        return Task.Run(ExecuteAsync).Result;
    }

    public async Task<bool> ExecuteAsync()
    {
        var baselineTask = Archive.Create(BaselineArchive.ItemSpec);
        var testTask = Archive.Create(TestArchive.ItemSpec);
        Task.WaitAll(baselineTask, testTask);
        using var baseline = await baselineTask;
        using var test = await testTask;
        var baselineFiles = baseline.GetFileNames();
        var testFiles = test.GetFileNames();
        ContentDifferences =
            GetDiffs(baselineFiles, testFiles, PathWithVersions.Equal, PathWithVersions.GetVersionlessPath)
            .Select(FromDiff)
            .ToArray();
        return true;
    }

    static ITaskItem FromDiff((string, DifferenceKind) diff)
    {
        var item = new TaskItem(diff.Item1);
        item.SetMetadata("Kind", Enum.GetName(diff.Item2));
        return item;
    }

    public enum DifferenceKind
    {
        Added,
        Removed,
        Unchanged
    }

    public static List<(string, DifferenceKind DifferenceKind)> GetDiffs(
        string[] originalPathsWithVersions,
        string[] modifiedPathsWithVersions,
        Func<string, string, bool> equalityComparer,
        Func<string, string>? formatter = null)
    {
        formatter ??= static s => s;
        // Edit distance algorithm: https://en.wikipedia.org/wiki/Longest_common_subsequence

        int[,] dp = new int[originalPathsWithVersions.Length + 1, modifiedPathsWithVersions.Length + 1];

        // Initialize first row and column
        for (int i = 0; i <= originalPathsWithVersions.Length; i++)
        {
            dp[i, 0] = i;
        }
        for (int j = 0; j <= modifiedPathsWithVersions.Length; j++)
        {
            dp[0, j] = j;
        }

        // Compute edit distance
        for (int i = 1; i <= originalPathsWithVersions.Length; i++)
        {
            for (int j = 1; j <= modifiedPathsWithVersions.Length; j++)
            {
                if (equalityComparer(originalPathsWithVersions[i - 1], modifiedPathsWithVersions[j - 1]))
                {
                    dp[i, j] = dp[i - 1, j - 1];
                }
                else
                {
                    dp[i, j] = 1 + Math.Min(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        // Trace back the edits
        int row = originalPathsWithVersions.Length;
        int col = modifiedPathsWithVersions.Length;

        List<(string, DifferenceKind)> formattedDiff = [];
        while (row > 0 || col > 0)
        {
            var baselineItem = originalPathsWithVersions[row - 1];
            var testItem = modifiedPathsWithVersions[col - 1];
            if (row > 0 && col > 0 && PathWithVersions.Equal(baselineItem, testItem))
            {
                formattedDiff.Add((formatter(originalPathsWithVersions[row - 1]), DifferenceKind.Unchanged));
                row--;
                col--;
            }
            else if (col > 0 && (row == 0 || dp[row, col - 1] <= dp[row - 1, col]))
            {
                formattedDiff.Add((formatter(modifiedPathsWithVersions[col - 1]), DifferenceKind.Added));
                col--;
            }
            else if (row > 0 && (col == 0 || dp[row, col - 1] > dp[row - 1, col]))
            {
                formattedDiff.Add((formatter(originalPathsWithVersions[row - 1]), DifferenceKind.Removed));
                row--;
            }
            else
            {
                throw new UnreachableException();
            }
        }
        formattedDiff.Reverse();
        return formattedDiff;
    }
}
