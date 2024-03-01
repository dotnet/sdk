// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

static class Diff
{
    public static ITaskItem TaskItemFromDiff((string, DifferenceKind) diff)
    {
        var item = new TaskItem(diff.Item1);
        item.SetMetadata("Kind", Enum.GetName(diff.Item2));
        return item;
    }

    public enum DifferenceKind
    {
        /// <summary>
        /// Present in the test but not in the baseline
        /// </summary>
        Added,

        /// <summary>
        /// Present in the baseline but not in the test
        /// </summary>
        Removed,

        /// <summary>
        /// Present in both the baseline and test
        /// </summary>
        Unchanged
    }

    /// <summary>
    /// Uses the Longest Common Subsequence algorithm (as used in 'git diff') to find the differences between two lists of strings.
    /// Returns a list of the joined lists with the differences marked as either added or removed.
    /// </summary>
    public static List<(string, DifferenceKind DifferenceKind)> GetDiffs(
        Span<string> baselineSequence,
        Span<string> testSequence,
        Func<string, string, bool> equalityComparer,
        Func<string, string>? formatter = null,
        CancellationToken cancellationToken = default)
    {
        // Edit distance algorithm: https://en.wikipedia.org/wiki/Longest_common_subsequence
        // cancellationToken.ThrowIfCancellationRequested();
        formatter ??= static s => s;

        // Optimization: remove common prefix
        int i = 0;
        List<(string, DifferenceKind)> prefix = [];
        while (i < baselineSequence.Length && i < testSequence.Length && equalityComparer(baselineSequence[i], testSequence[i]))
        {
            prefix.Add((formatter(baselineSequence[i]), DifferenceKind.Unchanged));
            i++;
        }

        baselineSequence = baselineSequence[i..];
        testSequence = testSequence[i..];

        // Initialize first row and column
        int[,] m = new int[baselineSequence.Length + 1, testSequence.Length + 1];
        for (i = 0; i <= baselineSequence.Length; i++)
        {
            m[i, 0] = i;
        }
        for (i = 0; i <= testSequence.Length; i++)
        {
            m[0, i] = i;
        }

        // Compute edit distance
        for (i = 1; i <= baselineSequence.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int j = 1; j <= testSequence.Length; j++)
            {
                if (equalityComparer(baselineSequence[i - 1], testSequence[j - 1]))
                {
                    m[i, j] = m[i - 1, j - 1];
                }
                else
                {
                    m[i, j] = 1 + Math.Min(m[i - 1, j], m[i, j - 1]);
                }
            }
        }

        // Trace back the edits
        int row = baselineSequence.Length;
        int col = testSequence.Length;
        List<(string, DifferenceKind)> diff = [];
        while (row > 0 || col > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row > 0 && col > 0 && equalityComparer(baselineSequence[row - 1], testSequence[col - 1]))
            {
                diff.Add((formatter(baselineSequence[row - 1]), DifferenceKind.Unchanged));
                row--;
                col--;
            }
            else if (col > 0 && (row == 0 || m[row, col - 1] <= m[row - 1, col]))
            {
                diff.Add((formatter(testSequence[col - 1]), DifferenceKind.Added));
                col--;
            }
            else if (row > 0 && (col == 0 || m[row, col - 1] > m[row - 1, col]))
            {
                diff.Add((formatter(baselineSequence[row - 1]), DifferenceKind.Removed));
                row--;
            }
            else
            {
                throw new UnreachableException();
            }
        }
        diff.Reverse();
        prefix.AddRange(diff);
        return prefix;
    }

}
