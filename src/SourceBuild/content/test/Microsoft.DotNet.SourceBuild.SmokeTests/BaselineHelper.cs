// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests
{
    internal class BaselineHelper
    {
        private const string SemanticVersionPlaceholder = "x.y.z";
        private const string SemanticVersionPlaceholderMatchingPattern = "*.*.*"; // wildcard pattern used to match on the version represented by the placeholder
        private const string NonSemanticVersionPlaceholder = "x.y";
        private const string NonSemanticVersionPlaceholderMatchingPattern = "*.*"; // wildcard pattern used to match on the version represented by the placeholder

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

        public static void CompareBaselineContents(string baselineFileName, string actualContents, ITestOutputHelper outputHelper, bool warnOnDiffs = false, string baselineSubDir = "")
        {
            string actualFilePath = Path.Combine(TestBase.LogsDirectory, $"Updated{baselineFileName}");
            File.WriteAllText(actualFilePath, actualContents);

            CompareFiles(GetBaselineFilePath(baselineFileName, baselineSubDir), actualFilePath, outputHelper, warnOnDiffs);
        }

        public static void CompareFiles(string expectedFilePath, string actualFilePath, ITestOutputHelper outputHelper, bool warnOnDiffs = false)
        {
            string baselineFileText = File.ReadAllText(expectedFilePath).Trim();
            string actualFileText = File.ReadAllText(actualFilePath).Trim();

            string? message = null;

            if (baselineFileText != actualFileText)
            {
                // Retrieve a diff in order to provide a UX which calls out the diffs.
                string diff = DiffFiles(expectedFilePath, actualFilePath, outputHelper);
                string prefix = warnOnDiffs ? "##vso[task.logissue type=warning;]" : string.Empty;
                message = $"{Environment.NewLine}{prefix}Expected file '{expectedFilePath}' does not match actual file '{actualFilePath}`.  {Environment.NewLine}"
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

            return diffResult.StdOut;
        }

        public static string GetAssetsDirectory() => Path.Combine(Directory.GetCurrentDirectory(), "assets");

        public static string GetBaselineFilePath(string baselineFileName, string baselineSubDir = "") =>
            Path.Combine(GetAssetsDirectory(), "baselines", baselineSubDir, baselineFileName);

        public static string RemoveRids(string diff, bool isPortable = false) =>
            isPortable ? diff.Replace(Config.PortableRid, "portable-rid") : diff.Replace(Config.TargetRid, "banana-rid");

        public static string RemoveVersions(string source)
        {
            // Remove version numbers for examples like "roslyn4.1", "net8.0", and "netstandard2.1".
            string pathSeparator = Regex.Escape(Path.DirectorySeparatorChar.ToString());
            string result = Regex.Replace(source, $@"{pathSeparator}(net|roslyn)[1-9]+\.[0-9]+{pathSeparator}", match =>
            {
                string wordPart = match.Groups[1].Value;
                return $"{Path.DirectorySeparatorChar}{wordPart}{NonSemanticVersionPlaceholder}{Path.DirectorySeparatorChar}";
            });
        
            // Remove semantic versions
            // Regex source: https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
            // The regex from https://semver.org has been modified to account for the following:
                // - The version should be preceded by a path separator, '.', '-', or '/'
                // - The version should match a release identifier that begins with '.' or '-'
                // - The version may have one or more release identifiers that begin with '.' or '-'
                // - The version should end before a path separator, '.', '-', or '/'
            Regex semanticVersionRegex = new(
                @"(?<=[./-])(0|[1-9]\d*)\.(0|[1-9]\d*)(\.(0|[1-9]\d*))+"
                + @"(((?:[-.]((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)))+"
                + @"(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?"
                + @"(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?(?=[/.-])");
            return semanticVersionRegex.Replace(result, SemanticVersionPlaceholder);
        }

        /// <summary>
        /// This returns a <see cref="Matcher"/> that can be used to match on a path whose versions have been removed via
        /// <see cref="RemoveVersions(string)"/>.
        /// </summary>
        public static Matcher GetFileMatcherFromPath(string path)
        {
            path = path
                .Replace(SemanticVersionPlaceholder, SemanticVersionPlaceholderMatchingPattern)
                .Replace(NonSemanticVersionPlaceholder, NonSemanticVersionPlaceholderMatchingPattern);
            Matcher matcher = new();
            matcher.AddInclude(path);
            return matcher;
        }
    }
}
