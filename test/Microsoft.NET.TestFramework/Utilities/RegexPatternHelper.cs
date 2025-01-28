// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Common;

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class RegexPatternHelper
    {
        public static string GenerateProjectRegexPattern(string projectName, string result, bool useCurrentVersion, string? exitCode = null)
        {
            string version = useCurrentVersion ? ToolsetInfo.CurrentTargetFramework : DotnetVersionHelper.GetPreviousDotnetVersion();
            string exitCodePattern = exitCode == null ? string.Empty : $@".*\s+Exit code: {exitCode}";
            return $@".+{PathUtility.GetDirectorySeparatorChar()}{version}{PathUtility.GetDirectorySeparatorChar()}{projectName}\.dll\s+\({version}\|[a-zA-Z][1-9]+\)\s{result}{exitCodePattern}";
        }

        public static string GenerateProjectRegexPattern(string projectName, bool useCurrentVersion, string prefix, List<string>? suffix = null)
        {
            string version = useCurrentVersion ? ToolsetInfo.CurrentTargetFramework : DotnetVersionHelper.GetPreviousDotnetVersion();
            string pattern = $@"{prefix}.*{PathUtility.GetDirectorySeparatorChar()}{projectName}\.dll\s+\({version}\|[a-zA-Z][1-9]+\)";

            if (suffix == null)
            {
                return pattern;
            }

            foreach (string s in suffix)
            {
                pattern += $@"\s+{s}";
            }

            return pattern;
        }
    }
}
