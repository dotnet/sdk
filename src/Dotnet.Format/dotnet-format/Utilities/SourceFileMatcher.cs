// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal sealed class SourceFileMatcher
    {
        private static string[] AllFilesList => new[] { @"**/*.*" };

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
    }
}
