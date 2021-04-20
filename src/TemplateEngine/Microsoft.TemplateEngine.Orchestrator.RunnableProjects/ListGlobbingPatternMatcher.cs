// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using System.Text;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class ListGlobbingPatternMatcher : IPathMatcher
    {
        internal ListGlobbingPatternMatcher(IList<string> patternList)
        {
            List<IPathMatcher> pathMatchers = new List<IPathMatcher>();

            foreach (string pattern in patternList)
            {
                pathMatchers.Add(new GlobbingPatternMatcher(pattern));
            }

            _pathMatchers = pathMatchers;
        }

        private readonly IReadOnlyList<IPathMatcher> _pathMatchers;

        public string Pattern
        {
            get
            {
                if (_displayPattern == null)
                {
                    StringBuilder displaySB = new StringBuilder(128);
                    displaySB.AppendLine("Composite matcher - matches any of these:");

                    foreach (IPathMatcher matcher in _pathMatchers)
                    {
                        displaySB.AppendLine($"\t{matcher.Pattern}");
                    }

                    _displayPattern = displaySB.ToString();
                }

                return _displayPattern;
            }
        }
        private string _displayPattern;

        public bool IsMatch(string path)
        {
            foreach (IPathMatcher matcher in _pathMatchers)
            {
                if (matcher.IsMatch(path))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
