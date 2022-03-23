// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests
{
    internal class BaselineHelper
    {
        public static void CompareEntries(string baselineFileName, IOrderedEnumerable<string> actualEntries)
        {
            IEnumerable<string> baseline = File.ReadAllLines(GetBaselineFilePath(baselineFileName));
            string[] missingEntries = actualEntries.Except(baseline).ToArray();
            string[] extraEntries = baseline.Except(actualEntries).ToArray();

            string? message = null;
            if (missingEntries.Length > 0)
            {
                message = $"Missing entries in '{baselineFileName}' baseline: {Environment.NewLine}{string.Join(Environment.NewLine, missingEntries)}{Environment.NewLine}{Environment.NewLine}";
            }

            if (extraEntries.Length > 0)
            {
                message += $"Extra entries in '{baselineFileName}' baseline: {Environment.NewLine}{string.Join(Environment.NewLine, extraEntries)}{Environment.NewLine}{Environment.NewLine}";
            }

            Assert.Null(message);
        }

        public static void CompareContents(string baselineFileName, string actualContents, ITestOutputHelper outputHelper, bool warnOnDiffs = false)
        {
            string baselineFilePath = GetBaselineFilePath(baselineFileName);

            string actualFilePath = Path.Combine(Environment.CurrentDirectory, $"{baselineFileName}");
            File.WriteAllText(actualFilePath, actualContents);

            CompareFiles(baselineFilePath, actualFilePath, outputHelper, warnOnDiffs);
        }

        public static void CompareFiles(string baselineFilePath, string actualFilePath, ITestOutputHelper outputHelper, bool warnOnDiffs = false)
        {
            string baselineFileText = File.ReadAllText(baselineFilePath);
            string actualFileText = File.ReadAllText(actualFilePath);

            string? message = null;

            if (baselineFileText != actualFileText)
            {
                // Retrieve a diff in order to provide a UX which calls out the diffs.
                string diff = DiffFiles(baselineFilePath, actualFilePath, outputHelper);
                string prefix = warnOnDiffs ? "##vso[task.logissue type=warning;]" : string.Empty;
                message = $"{Environment.NewLine}{prefix}Baseline '{baselineFilePath}' does not match actual '{actualFilePath}`.  {Environment.NewLine}"
                    + $"{diff}{Environment.NewLine}";

                if (warnOnDiffs)
                {
                    outputHelper.WriteLine(message);
                    outputHelper.WriteLine("##vso[task.complete result=SucceededWithIssues;]");
                }
            }

            if (!warnOnDiffs)
            {
                Assert.Null(message);
            }
        }

        public static string DiffFiles(string file1Path, string file2Path, ITestOutputHelper outputHelper)
        {
            (Process Process, string StdOut, string StdErr) diffResult =
                ExecuteHelper.ExecuteProcess("git", $"diff --no-index {file1Path} {file2Path}", outputHelper);
            Assert.Equal(1, diffResult.Process.ExitCode);

            return diffResult.StdOut;
        }

        public static string GetAssetsDirectory() => Path.Combine(Directory.GetCurrentDirectory(), "assets");

        private static string GetBaselineFilePath(string baselineFileName) => Path.Combine(GetAssetsDirectory(), "baselines", baselineFileName);
    }
}
