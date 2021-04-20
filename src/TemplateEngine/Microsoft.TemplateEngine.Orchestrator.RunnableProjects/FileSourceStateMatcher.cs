// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class FileSourceStateMatcher : IPathMatcher
    {
        internal FileSourceStateMatcher(FileDispositionStates checkState, FileSourceHierarchicalPathMatcher stateMatcher)
        {
            _checkState = checkState;
            _stateMatcher = stateMatcher;
        }

        public bool IsMatch(string path)
        {
            return _stateMatcher.Evaluate(path).Has(_checkState);
        }

        public string Pattern
        {
            get
            {
                return $"Composite matcher for disposition: {_checkState.ToString()}";
            }
        }

        private FileDispositionStates _checkState;
        private FileSourceHierarchicalPathMatcher _stateMatcher;
    }
}
