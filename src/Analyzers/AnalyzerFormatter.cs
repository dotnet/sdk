// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerFormatter : ICodeFormatter
    {
        private readonly string _name;
        private readonly IAnalyzerFinder _finder;
        private readonly IAnalyzerRunner _runner;
        private readonly ICodeFixApplier _applier;

        public AnalyzerFormatter(
            string name,
            IAnalyzerFinder finder,
            IAnalyzerRunner runner,
            ICodeFixApplier applier)
        {
            _name = name;
            _finder = finder;
            _runner = runner;
            _applier = applier;
        }

        public async Task<Solution> FormatAsync(
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var analyzersAndFixers = _finder.GetAnalyzersAndFixers(solution, formatOptions, logger);
            if (analyzersAndFixers.Length == 0)
            {
                return solution;
            }

            var analysisStopwatch = Stopwatch.StartNew();
            logger.LogTrace($"Running {_name} analysis.");

            var formattablePaths = formattableDocuments.Select(id => solution.GetDocument(id)!.FilePath)
                    .OfType<string>().ToImmutableHashSet();

            logger.LogTrace("Determining diagnostics...");

            var allAnalyzers = analyzersAndFixers.Select(pair => pair.Analyzer).ToImmutableArray();
            var projectAnalyzers = await _finder.FilterBySeverityAsync(solution.Projects, allAnalyzers, formattablePaths, formatOptions, cancellationToken).ConfigureAwait(false);

            var projectDiagnostics = await GetProjectDiagnosticsAsync(solution, projectAnalyzers, formattablePaths, formatOptions, logger, formattedFiles, cancellationToken).ConfigureAwait(false);

            var projectDiagnosticsMS = analysisStopwatch.ElapsedMilliseconds;
            logger.LogTrace(Resources.Complete_in_0_ms, projectDiagnosticsMS);

            logger.LogTrace("Fixing diagnostics...");

            solution = await FixDiagnosticsAsync(solution, analyzersAndFixers, projectDiagnostics, formattablePaths, logger, cancellationToken).ConfigureAwait(false);

            var fixDiagnosticsMS = analysisStopwatch.ElapsedMilliseconds - projectDiagnosticsMS;
            logger.LogTrace(Resources.Complete_in_0_ms, fixDiagnosticsMS);

            logger.LogTrace("Analysis complete in {0}ms.", analysisStopwatch.ElapsedMilliseconds);

            return solution;
        }

        private async Task<ImmutableDictionary<ProjectId, ImmutableHashSet<string>>> GetProjectDiagnosticsAsync(
            Solution solution,
            ImmutableDictionary<Project, ImmutableArray<DiagnosticAnalyzer>> projectAnalyzers,
            ImmutableHashSet<string> formattablePaths,
            FormatOptions options,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var result = new CodeAnalysisResult();
            foreach (var project in solution.Projects)
            {
                var analyzers = projectAnalyzers[project];
                if (analyzers.Length == 0)
                {
                    continue;
                }

                await _runner.RunCodeAnalysisAsync(result, analyzers, project, formattablePaths, logger, cancellationToken).ConfigureAwait(false);
            }

            LogDiagnosticLocations(solution, result.Diagnostics.SelectMany(kvp => kvp.Value), options.WorkspaceFilePath, options.ChangesAreErrors, logger, formattedFiles);

            return result.Diagnostics.ToImmutableDictionary(kvp => kvp.Key.Id, kvp => kvp.Value.Select(diagnostic => diagnostic.Id).ToImmutableHashSet());

            static void LogDiagnosticLocations(Solution solution, IEnumerable<Diagnostic> diagnostics, string workspacePath, bool changesAreErrors, ILogger logger, List<FormattedFile> formattedFiles)
            {
                var workspaceFolder = Path.GetDirectoryName(workspacePath);

                foreach (var diagnostic in diagnostics)
                {
                    var message = diagnostic.GetMessage();
                    var filePath = diagnostic.Location.SourceTree?.FilePath;
                    var document = solution.GetDocument(diagnostic.Location.SourceTree);

                    var mappedLineSpan = diagnostic.Location.GetMappedLineSpan();
                    var changePosition = mappedLineSpan.StartLinePosition;

                    var formatMessage = $"{Path.GetRelativePath(workspaceFolder, filePath)}({changePosition.Line + 1},{changePosition.Character + 1}): {message}";
                    formattedFiles.Add(new FormattedFile(document!, new[] { new FileChange(changePosition, message) }));

                    if (changesAreErrors)
                    {
                        logger.LogError(formatMessage);
                    }
                    else
                    {
                        logger.LogWarning(formatMessage);
                    }
                }
            }
        }

        private async Task<Solution> FixDiagnosticsAsync(
            Solution solution,
            ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> analyzersAndFixers,
            ImmutableDictionary<ProjectId, ImmutableHashSet<string>> projectDiagnostics,
            ImmutableHashSet<string> formattablePaths,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // we need to run each codefix iteratively so ensure that all diagnostics are found and fixed
            foreach (var (analyzer, codefix) in analyzersAndFixers)
            {
                var result = new CodeAnalysisResult();
                foreach (var project in solution.Projects)
                {
                    if (!projectDiagnostics.TryGetValue(project.Id, out var diagnosticIds) ||
                        !analyzer.SupportedDiagnostics.Any(diagnostic => diagnosticIds.Contains(diagnostic.Id)))
                    {
                        continue;
                    }

                    await _runner.RunCodeAnalysisAsync(result, analyzer, project, formattablePaths, logger, cancellationToken).ConfigureAwait(false);
                }

                var hasDiagnostics = result.Diagnostics.Any(kvp => kvp.Value.Count > 0);
                if (hasDiagnostics && codefix != null)
                {
                    solution = await _applier.ApplyCodeFixesAsync(solution, result, codefix, logger, cancellationToken).ConfigureAwait(false);
                    var changedSolution = await _applier.ApplyCodeFixesAsync(solution, result, codefix, logger, cancellationToken).ConfigureAwait(false);
                    if (changedSolution.GetChanges(solution).Any())
                    {
                        solution = changedSolution;
                    }
                }
            }

            return solution;
        }
    }
}
