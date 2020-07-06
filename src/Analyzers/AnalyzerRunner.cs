// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal partial class AnalyzerRunner : IAnalyzerRunner
    {
        private readonly bool _includeComplilerDiagnostics;

        public AnalyzerRunner(bool includeCompilerDiagnostics)
        {
            _includeComplilerDiagnostics = includeCompilerDiagnostics;
        }

        public Task RunCodeAnalysisAsync(
            CodeAnalysisResult result,
            DiagnosticAnalyzer analyzers,
            Project project,
            ImmutableHashSet<string> formattableDocumentPaths,
            DiagnosticSeverity severity,
            ILogger logger,
            CancellationToken cancellationToken)
            => RunCodeAnalysisAsync(result, ImmutableArray.Create(analyzers), project, formattableDocumentPaths, severity, logger, cancellationToken);

        public async Task RunCodeAnalysisAsync(
            CodeAnalysisResult result,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            Project project,
            ImmutableHashSet<string> formattableDocumentPaths,
            DiagnosticSeverity severity,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                return;
            }

            // If are not running any analyzers and are not reporting compiler diagnostics, then there is
            // nothing to report.
            if (analyzers.IsEmpty && !_includeComplilerDiagnostics)
            {
                return;
            }

            ImmutableArray<Diagnostic> diagnostics;
            if (analyzers.IsEmpty)
            {
                diagnostics = compilation.GetDiagnostics(cancellationToken);
            }
            else
            {
                var analyzerOptions = new CompilationWithAnalyzersOptions(
                    project.AnalyzerOptions,
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false);
                var analyzerCompilation = compilation.WithAnalyzers(analyzers, analyzerOptions);
                diagnostics = _includeComplilerDiagnostics
                    ? await analyzerCompilation.GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false)
                    : await analyzerCompilation.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
            }

            // filter diagnostics
            foreach (var diagnostic in diagnostics)
            {
                if (!diagnostic.IsSuppressed &&
                    diagnostic.Severity >= severity &&
                    diagnostic.Location.IsInSource &&
                    diagnostic.Location.SourceTree != null &&
                    formattableDocumentPaths.Contains(diagnostic.Location.SourceTree.FilePath))
                {
                    result.AddDiagnostic(project, diagnostic);
                }
            }
        }
    }
}
