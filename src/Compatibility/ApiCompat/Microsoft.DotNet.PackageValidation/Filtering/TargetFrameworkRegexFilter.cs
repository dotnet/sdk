// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.PackageValidation.Filtering
{
    /// <summary>
    /// Helper to check for excluded target frameworks based on a regex pattern.
    /// </summary>
    /// <param name="excludeTargetFrameworks">String pattern to exclude target frameworks. Multiple target frameworks must be separated with the ';' character. The glob '*' character is supported.</param>
    public class TargetFrameworkRegexFilter(string excludeTargetFrameworks) : ITargetFrameworkRegexFilter
    {
        private const char TargetFrameworkDelimiter = ';';
        private readonly Regex? _excludeTargetFrameworks = TransformPatternsToRegexList(excludeTargetFrameworks);
        private readonly HashSet<string> _foundExcludedTargetFrameworks = [];

        /// <inheritdoc/>
        public IReadOnlyCollection<string> FoundExcludedTargetFrameworks => _foundExcludedTargetFrameworks;

        /// <inheritdoc/>
        public bool IsExcluded(string targetFramework)
        {
            if (_foundExcludedTargetFrameworks.Contains(targetFramework))
            {
                return true;
            }

            // Skip target frameworks that are excluded.
            if (_excludeTargetFrameworks is not null && _excludeTargetFrameworks.IsMatch(targetFramework))
            {
                _foundExcludedTargetFrameworks.Add(targetFramework);
                return true;
            }

            return false;
        }

        private static Regex? TransformPatternsToRegexList(string patterns)
        {
            if (patterns == string.Empty)
            {
                return null;
            }

#if NET
            string pattern = patterns.Split(TargetFrameworkDelimiter, StringSplitOptions.RemoveEmptyEntries)
#else
            string pattern = patterns.Split(new char[] { TargetFrameworkDelimiter }, StringSplitOptions.RemoveEmptyEntries)
#endif
                .Select(p => Regex.Escape(p).Replace("\\*", ".*"))
                .Aggregate((p1, p2) => p1 + "|" + p2);
            pattern = $"^(?:{pattern})$";

            return new Regex(pattern,
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase |
                RegexOptions.Compiled
#if NET
                | RegexOptions.NonBacktracking
#endif
                );
        }
    }
}
