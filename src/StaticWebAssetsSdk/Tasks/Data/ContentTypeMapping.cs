// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    internal struct ContentTypeMapping
    {
        private Matcher _matcher;

        public ContentTypeMapping(string mimeType, string cache, string pattern, int priority)
        {
            Pattern = pattern;
            MimeType = mimeType;
            Cache = cache;
            Priority = priority;
        }

        public string Pattern { get; set; }

        public string MimeType { get; set; }

        public string Cache { get; set; }

        public int Priority { get; }

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
    }
}
