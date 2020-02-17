// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class SourceFileMatcher
    {
        private static IEnumerable<string> AllFilesList => new[] { @"**/*.*" };

        public static Matcher CreateMatcher(IEnumerable<string> include, IEnumerable<string> exclude)
        {
            var fileMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            fileMatcher.AddIncludePatterns(include.Any() ? include : AllFilesList);
            fileMatcher.AddExcludePatterns(exclude);
            return fileMatcher;
        }
    }
}
