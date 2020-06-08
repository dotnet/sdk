// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    interface IAnalyzerFinder
    {
        ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger);

        Task<ImmutableDictionary<Project, ImmutableArray<DiagnosticAnalyzer>>> FilterBySeverityAsync(
            IEnumerable<Project> projects,
            ImmutableArray<DiagnosticAnalyzer> allAnalyzers,
            ImmutableHashSet<string> formattablePaths,
            FormatOptions formatOptions,
            CancellationToken cancellationToken);
    }
}
