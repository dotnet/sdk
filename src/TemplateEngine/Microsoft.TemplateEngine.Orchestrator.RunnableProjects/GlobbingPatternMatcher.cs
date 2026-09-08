// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class GlobbingPatternMatcher : IPathMatcher
    {
        private readonly Glob _pattern;

        internal GlobbingPatternMatcher(string pattern, bool canBeNameOnlyMatch = true)
        {
            Pattern = pattern;
            _pattern = Glob.Parse(pattern, canBeNameOnlyMatch);
        }

        public string Pattern { get; }

        public bool IsMatch(string path)
        {
            return _pattern.IsMatch(path);
        }
    }
}
