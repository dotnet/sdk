// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal sealed class SourceFileMatcher
    {
        private static string[] AllFilesList => new[] { @"**/*.*" };

        public static SourceFileMatcher CreateMatcher(string[] include, string[] exclude)
            => new SourceFileMatcher(include.Length > 0 ? include : AllFilesList, exclude);

        private readonly Matcher _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);

        public ImmutableArray<string> Include { get; }
        public ImmutableArray<string> Exclude { get; }

        private SourceFileMatcher(string[] include, string[] exclude)
        {
            Include = include.ToImmutableArray();
            Exclude = exclude.ToImmutableArray();

            _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            _matcher.AddIncludePatterns(Include);
            _matcher.AddExcludePatterns(Exclude);
        }

        public PatternMatchingResult Match(string filePath)
            => _matcher.Match(filePath);

        public IEnumerable<string> GetResultsInFullPath(string directoryPath)
            => _matcher.GetResultsInFullPath(directoryPath);
    }
}
