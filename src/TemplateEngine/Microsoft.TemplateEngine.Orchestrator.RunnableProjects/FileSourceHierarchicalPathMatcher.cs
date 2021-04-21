// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    [Flags]
    internal enum FileDispositionStates
    {
        None = 0,
        Include = 0x01,
        CopyOnly = 0x02,
        Exclude = 0x04
    }

    internal static class EnumExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has(this FileDispositionStates value, FileDispositionStates check)
        {
            return (value & check) == check;
        }
    }

    internal class FileSourceHierarchicalPathMatcher
    {
        private IReadOnlyList<FileSourceEvaluablePathMatcher> _evaluators;

        // Caching the most recent result.
        private string _cachedEvaluatedPath;

        private FileDispositionStates _cachedStatesForFile;

        internal FileSourceHierarchicalPathMatcher(FileSourceMatchInfo matchInfo)
        {
            List<FileSourceEvaluablePathMatcher> evaluators = new List<FileSourceEvaluablePathMatcher>();

            for (int i = matchInfo.Modifiers.Count - 1; i >= 0; i--)
            {
                evaluators.Add(new FileSourceEvaluablePathMatcher(matchInfo.Modifiers[i]));
            }

            evaluators.Add(new FileSourceEvaluablePathMatcher(matchInfo.TopLevelEvaluable));
            _evaluators = evaluators;

            _cachedEvaluatedPath = string.Empty;
            _cachedStatesForFile = FileDispositionStates.None;
        }

        internal FileDispositionStates Evaluate(string path)
        {
            if (!string.Equals(path, _cachedEvaluatedPath, StringComparison.Ordinal))
            {
                _cachedStatesForFile = EvaluatePath(path);
                _cachedEvaluatedPath = path;
            }

            return _cachedStatesForFile;
        }

        private FileDispositionStates EvaluatePath(string path)
        {
            FileDispositionStates continuationReason = FileDispositionStates.None;

            foreach (FileSourceEvaluablePathMatcher evaluator in _evaluators)
            {
                FileDispositionStates state = evaluator.Evaluate(path);

                if (state.Has(FileDispositionStates.Exclude))
                {
                    return FileDispositionStates.Exclude;
                }
                else if (state.Has(FileDispositionStates.CopyOnly))
                {
                    if (state.Has(FileDispositionStates.Include))
                    {
                        return FileDispositionStates.CopyOnly | FileDispositionStates.Include;
                    }

                    continuationReason = FileDispositionStates.CopyOnly;
                }
                else if (state.Has(FileDispositionStates.Include))
                {
                    if (continuationReason == FileDispositionStates.CopyOnly)
                    {
                        return FileDispositionStates.CopyOnly | FileDispositionStates.Include;
                    }

                    return FileDispositionStates.Include;
                }
            }

            return FileDispositionStates.None;
        }

        private class FileSourceEvaluablePathMatcher
        {
            private IReadOnlyList<IPathMatcher> _include;

            private IReadOnlyList<IPathMatcher> _exclude;

            private IReadOnlyList<IPathMatcher> _copyOnly;

            internal FileSourceEvaluablePathMatcher(FileSourceEvaluable evaluable)
            {
                _include = SetupMatcherList(evaluable.Include);
                _exclude = SetupMatcherList(evaluable.Exclude);
                _copyOnly = SetupMatcherList(evaluable.CopyOnly);
            }

            internal FileDispositionStates Evaluate(string path)
            {
                if (_exclude.Any(x => x.IsMatch(path)))
                {
                    return FileDispositionStates.Exclude;
                }

                bool include = _include.Any(x => x.IsMatch(path));

                if (_copyOnly.Any(x => x.IsMatch(path)))
                {
                    if (include)
                    {
                        return FileDispositionStates.CopyOnly | FileDispositionStates.Include;
                    }
                    else
                    {
                        return FileDispositionStates.CopyOnly;
                    }
                }

                if (include)
                {
                    return FileDispositionStates.Include;
                }

                return FileDispositionStates.None;
            }

            private static IReadOnlyList<IPathMatcher> SetupMatcherList(IReadOnlyList<string> fileSources)
            {
                int expect = fileSources?.Count ?? 0;
                List<IPathMatcher> paths = new List<IPathMatcher>(expect);
                if (fileSources != null && expect > 0)
                {
                    foreach (string source in fileSources)
                    {
                        paths.Add(new GlobbingPatternMatcher(source, false));
                    }
                }

                return paths;
            }
        }
    }
}
