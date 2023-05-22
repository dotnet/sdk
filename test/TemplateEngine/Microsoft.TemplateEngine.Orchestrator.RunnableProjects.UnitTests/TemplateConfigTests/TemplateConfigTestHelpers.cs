// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Mocks;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public static class TemplateConfigTestHelpers
    {
        internal static void SetupFileSourceMatchersOnGlobalRunSpec(MockGlobalRunSpec runSpec, FileSourceMatchInfo source)
        {
            FileSourceHierarchicalPathMatcher matcher = new FileSourceHierarchicalPathMatcher(source);
            runSpec.Include = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Include, matcher) };
            runSpec.Exclude = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Exclude, matcher) };
            runSpec.CopyOnly = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.CopyOnly, matcher) };
            runSpec.Rename = source.Renames ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
