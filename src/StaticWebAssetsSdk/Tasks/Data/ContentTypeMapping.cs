// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    internal struct ContentTypeMapping(string mimeType, string cache, string pattern, int priority)
    {
        private Matcher _matcher;

        public string Pattern { get; set; } = pattern;

        public string MimeType { get; set; } = mimeType;

        public string Cache { get; set; } = cache;

        public int Priority { get; } = priority;

        internal static ContentTypeMapping FromTaskItem(ITaskItem contentTypeMappings) => new(
                contentTypeMappings.ItemSpec,
                contentTypeMappings.GetMetadata(nameof(Cache)),
                contentTypeMappings.GetMetadata(nameof(Pattern)),
                int.Parse(contentTypeMappings.GetMetadata(nameof(Priority))));

        internal bool Matches(string identity)
        {
            if (_matcher == null)
            {
                _matcher = new Matcher();
                _matcher.AddInclude(Pattern);
            }
            return _matcher.Match(identity).HasMatches;
        }

        private string GetDebuggerDisplay() => $"Pattern: {Pattern}, MimeType: {MimeType}, Cache: {Cache}, Priority: {Priority}";
    }
}
