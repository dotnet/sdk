// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
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

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                return;
            }

            // If we didn't get a useful set of references, then we really don't want to be trying to run analyzers.
            if (!await AllReferencedProjectsLoadedAsync(project, cancellationToken))
            {
                logger.LogWarning(Resources.Required_references_did_not_load_for_0_or_referenced_project_Run_dotnet_restore_prior_to_formatting, project.Name);
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

                diagnostics = await analyzerCompilation.GetAnalyzerDiagnosticsAsync(cancellationToken);
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

            // Checks each transitive dependency once via the solution's cached dependency graph. Recursing
            // through ProjectReferences instead revisits shared projects on every path through the graph,
            // which is exponential in the graph's depth.
            static async Task<bool> AllReferencedProjectsLoadedAsync(Project project, CancellationToken cancellationToken)
            {
                var solution = project.Solution;
                var dependencies = solution.GetProjectDependencyGraph().GetProjectsThatThisProjectTransitivelyDependsOn(project.Id);

                foreach (var projectId in dependencies.Prepend(project.Id))
                {
                    var dependency = solution.GetProject(projectId);
                    Debug.Assert(dependency is not null);

                    var compilation = await dependency.GetCompilationAsync(cancellationToken);
                    if (compilation is null)
                        return false;

                    // If we don't have a System.Object this project is clearly missing references and we shouldn't try to process it further
                    if (compilation.ObjectType.TypeKind == TypeKind.Error)
                        return false;
                }

                return true;
            }
        }
    }
}
