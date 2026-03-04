// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal partial class AnalyzerRunner : IAnalyzerRunner
    {
        public Task RunCodeAnalysisAsync(
            CodeAnalysisResult result,
            DiagnosticAnalyzer analyzers,
            Project project,
            ImmutableHashSet<string> formattableDocumentPaths,
            DiagnosticSeverity severity,
            ImmutableHashSet<string> fixableCompilerDiagnostics,
            ILogger logger,
            CancellationToken cancellationToken)
            => RunCodeAnalysisAsync(result, ImmutableArray.Create(analyzers), project, formattableDocumentPaths, severity, fixableCompilerDiagnostics, logger, cancellationToken);

        public async Task RunCodeAnalysisAsync(
            CodeAnalysisResult result,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            Project project,
            ImmutableHashSet<string> formattableDocumentPaths,
            DiagnosticSeverity severity,
            ImmutableHashSet<string> fixableCompilerDiagnostics,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // If are not running any analyzers and are not reporting compiler diagnostics, then there is
            // nothing to report.
            if (analyzers.IsEmpty && fixableCompilerDiagnostics.IsEmpty)
            {
                return;
            }

            // For projects targeting NetStandard, the Runtime references are resolved from the project.assets.json.
            // This file is generated during a `dotnet restore`.
            if (!AllReferencedProjectsLoaded(project))
            {
                logger.LogWarning(Resources.Required_references_did_not_load_for_0_or_referenced_project_Run_dotnet_restore_prior_to_formatting, project.Name);
                return;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                return;
            }

            var compilerDiagnostics = !fixableCompilerDiagnostics.IsEmpty
                ? compilation.GetDiagnostics(cancellationToken)
                    .Where(diagnostic => fixableCompilerDiagnostics.Contains(diagnostic.Id))
                    .ToImmutableArray()
                : ImmutableArray<Diagnostic>.Empty;

            ImmutableArray<Diagnostic> diagnostics;
            if (analyzers.IsEmpty)
            {
                diagnostics = compilerDiagnostics;
            }
            else
            {
                logger.LogDebug(Resources.Running_0_analyzers_on_1, analyzers.Length, project.Name);

                var analyzerOptions = new CompilationWithAnalyzersOptions(
                    project.AnalyzerOptions,
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false);
                var analyzerCompilation = compilation.WithAnalyzers(analyzers, analyzerOptions);

                diagnostics = await analyzerCompilation.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
                diagnostics = diagnostics.AddRange(compilerDiagnostics);
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

            return;

            static bool AllReferencedProjectsLoaded(Project project)
            {
                // Use mscorlib to represent Runtime references being loaded.
                if (!project.MetadataReferences.Any(reference => reference.Display?.EndsWith("mscorlib.dll") == true))
                {
                    return false;
                }

                return project.ProjectReferences
                    .Select(projectReference => project.Solution.GetProject(projectReference.ProjectId))
                    .All(referencedProject => referencedProject != null && AllReferencedProjectsLoaded(referencedProject));
            }
        }
    }
}
