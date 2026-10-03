// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal sealed class SourceFileMatcher
    {
        private static string[] AllFilesList => new[] { @"**/*.*" };

        // Directories that an MSBuild workspace would never turn into documents, so they can be
        // skipped while enumerating a workspace without running MSBuild.
        private static string[] FormattableIgnoredDirectories => new[]
        {
            @"**/.git/**",
            @"**/.vs/**",
            @"**/node_modules/**",
        };

        public static SourceFileMatcher CreateMatcher(string[] include, string[] exclude)
            => new SourceFileMatcher(include, exclude);

        private readonly Matcher _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        private readonly bool _shouldMatchAll;

        public ImmutableArray<string> Include { get; }
        public ImmutableArray<string> Exclude { get; }

        private SourceFileMatcher(string[] include, string[] exclude)
        {
            _shouldMatchAll = include.Length == 0 && exclude.Length == 0;

            Include = include.Length > 0
                ? include.ToImmutableArray()
                : AllFilesList.ToImmutableArray();
            Exclude = exclude.ToImmutableArray();

            _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            _matcher.AddIncludePatterns(Include);
            _matcher.AddExcludePatterns(Exclude);
        }

        public bool HasMatches(string filePath)
            => _shouldMatchAll || _matcher.Match(filePath).HasMatches;

        public IEnumerable<string> GetResultsInFullPath(string directoryPath)
            => _matcher.GetResultsInFullPath(directoryPath);

        /// <summary>
        /// Gets the files beneath <paramref name="directoryPath"/> that an MSBuild workspace could
        /// turn into documents, skipping the directories that would never be part of it. The
        /// include/exclude patterns are intentionally not applied here because those patterns are
        /// meant to be matched against absolute file paths (see <see cref="HasMatches(string)"/>),
        /// so callers filter the returned paths with <see cref="HasMatches(string)"/>.
        /// </summary>
        public IEnumerable<string> GetFormattableResultsInFullPath(string directoryPath)
        {
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddIncludePatterns(AllFilesList);
            matcher.AddExcludePatterns(FormattableIgnoredDirectories);
            return matcher.GetResultsInFullPath(directoryPath);
        }
    }
}
