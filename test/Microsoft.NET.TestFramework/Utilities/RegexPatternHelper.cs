// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class RegexPatternHelper
    {
        public static string GenerateProjectRegexPattern(string projectName, string result, bool useCurrentVersion, string configuration, string? exitCode = null, string? runtime = null)
        {
            string version = useCurrentVersion ? ToolsetInfo.CurrentTargetFramework : DotnetVersionHelper.GetPreviousDotnetVersion();

            string runtimeIdentifier = runtime is not null ?
                $"{runtime}{PathUtility.GetDirectorySeparatorChar()}" :
                string.Empty;

            string exitCodePattern = exitCode == null ? string.Empty : $@"[\s\S]*?Exit\s+code: {exitCode}";
            return $@".+{configuration}{PathUtility.GetDirectorySeparatorChar()}{version}{PathUtility.GetDirectorySeparatorChar()}{runtimeIdentifier}{projectName}(\.dll|\.exe)?\s+\({version}\|[a-zA-Z][1-9]+\)\s{result}{exitCodePattern}";
        }

        public static string GenerateProjectRegexPattern(string projectName, bool useCurrentVersion, string configuration, string prefix, List<string>? suffix = null, bool addVersionAndArchPattern = true)
        {
            string version = useCurrentVersion ? ToolsetInfo.CurrentTargetFramework : DotnetVersionHelper.GetPreviousDotnetVersion();
            string pattern = $@"{prefix}.*{configuration}{PathUtility.GetDirectorySeparatorChar()}{version}{PathUtility.GetDirectorySeparatorChar()}{projectName}(\.dll|\.exe)?";

            if (addVersionAndArchPattern)
            {
                pattern += @$"\s+\({version}\|[a-zA-Z][1-9]+\)";
            }

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
