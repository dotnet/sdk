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
        public static void Compare(string baselineFileName, IOrderedEnumerable<string> actualEntries)
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

        public static void Compare(string baselineFileName, string actual, ITestOutputHelper outputHelper)
        {
            string baselineFilePath = GetBaselineFilePath(baselineFileName);
            string baseline = File.ReadAllText(baselineFilePath);

            string? message = null;
            if (baseline != actual)
            {
                string actualBaselineFilePath = Path.Combine(Environment.CurrentDirectory, $"{baselineFileName}");
                File.WriteAllText(actualBaselineFilePath, actual);

                // Retrieve a diff in order to provide a UX which calls out the diffs.
                string diff = DiffFiles(baselineFilePath, actualBaselineFilePath, outputHelper);
                message = $"{Environment.NewLine}Baseline '{baselineFilePath}' does not match actual '{actualBaselineFilePath}`.  {Environment.NewLine}"
                    + $"{diff}{Environment.NewLine}";
            }

            Assert.Null(message);
        }

        public static string DiffFiles(string file1Path, string file2Path, ITestOutputHelper outputHelper)
        {
            (Process Process, string StdOut, string StdErr) diffResult =
                ExecuteHelper.ExecuteProcess("git", $"diff --no-index {file1Path} {file2Path}", outputHelper);
            Assert.Equal(1, diffResult.Process.ExitCode);

            return diffResult.StdOut;
        }

        private static string GetBaselineFilePath(string baselineFileName) => Path.Combine(Directory.GetCurrentDirectory(), "baselines", baselineFileName);
    }
}
