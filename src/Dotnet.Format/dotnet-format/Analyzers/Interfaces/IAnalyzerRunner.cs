// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal interface IAnalyzerRunner
    {
        Task RunCodeAnalysisAsync(
            CodeAnalysisResult result,
            DiagnosticAnalyzer analyzers,
            Project project,
            ImmutableHashSet<string> formattableDocumentPaths,
            DiagnosticSeverity severity,
            ImmutableHashSet<string> fixableCompilerDiagnostics,
            ILogger logger,
            CancellationToken cancellationToken);

        Task RunCodeAnalysisAsync(
            CodeAnalysisResult result,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            Project project,
            ImmutableHashSet<string> formattableDocumentPaths,
            DiagnosticSeverity severity,
            ImmutableHashSet<string> fixableCompilerDiagnostics,
            ILogger logger,
            CancellationToken cancellationToken);
    }
}
