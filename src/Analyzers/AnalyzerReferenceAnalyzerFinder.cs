// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerReferenceAnalyzerFinder : IAnalyzerFinder
    {
        public ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            if (!formatOptions.FixAnalyzers)
            {
                return ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)>.Empty;
            }

            var assemblies = solution.Projects
                .SelectMany(project => project.AnalyzerReferences.Select(reference => reference.FullPath))
                .Distinct()
                .Select(path => Assembly.LoadFrom(path));

            return AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies, logger);
        }

        public Task<ImmutableDictionary<Project, ImmutableArray<DiagnosticAnalyzer>>> FilterBySeverityAsync(
            IEnumerable<Project> projects,
            ImmutableArray<DiagnosticAnalyzer> allAnalyzers,
            ImmutableHashSet<string> formattablePaths,
            FormatOptions formatOptions,
            CancellationToken cancellationToken)
        {
            return AnalyzerFinderHelpers.FilterBySeverityAsync(projects, allAnalyzers, formattablePaths, formatOptions.AnalyzerSeverity, cancellationToken);
        }
    }
}
