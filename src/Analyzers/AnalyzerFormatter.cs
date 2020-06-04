// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerFormatter : ICodeFormatter
    {
        public FormatType FormatType => FormatType.CodeStyle;

        private readonly IAnalyzerFinder _finder;
        private readonly IAnalyzerRunner _runner;
        private readonly ICodeFixApplier _applier;

        public AnalyzerFormatter(
            IAnalyzerFinder finder,
            IAnalyzerRunner runner,
            ICodeFixApplier applier)
        {
            _finder = finder;
            _runner = runner;
            _applier = applier;
        }

        public async Task<Solution> FormatAsync(
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions options,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var analysisStopwatch = Stopwatch.StartNew();
            logger.LogTrace($"Analyzing code style.");

            if (!options.SaveFormattedFiles)
            {
                await LogDiagnosticsAsync(solution, formattableDocuments, options, logger, formattedFiles, cancellationToken);
            }
            else
            {
                solution = await FixDiagnosticsAsync(solution, formattableDocuments, logger, cancellationToken);
            }

            logger.LogTrace("Analysis complete in {0}ms.", analysisStopwatch.ElapsedMilliseconds);

            return solution;
        }

        private async Task LogDiagnosticsAsync(Solution solution, ImmutableArray<DocumentId> formattableDocuments, FormatOptions options, ILogger logger, List<FormattedFile> formattedFiles, CancellationToken cancellationToken)
        {
            var pairs = _finder.GetAnalyzersAndFixers();
            var paths = formattableDocuments.Select(id => solution.GetDocument(id)?.FilePath)
                .OfType<string>().ToImmutableArray();

            // no need to run codefixes as we won't persist the changes
            var analyzers = pairs.Select(x => x.Analyzer).ToImmutableArray();
            var result = new CodeAnalysisResult();
            await solution.Projects.ForEachAsync(async (project, token) =>
            {
                await _runner.RunCodeAnalysisAsync(result, analyzers, project, paths, logger, token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            LogDiagnosticLocations(result.Diagnostics.SelectMany(kvp => kvp.Value), options.WorkspaceFilePath, options.ChangesAreErrors, logger, formattedFiles);

            return;

            static void LogDiagnosticLocations(IEnumerable<Diagnostic> diagnostics, string workspacePath, bool changesAreErrors, ILogger logger, List<FormattedFile> formattedFiles)
            {
                var workspaceFolder = Path.GetDirectoryName(workspacePath);

                foreach (var diagnostic in diagnostics)
                {
                    var message = diagnostic.GetMessage();
                    var filePath = diagnostic.Location.SourceTree?.FilePath;

                    var mappedLineSpan = diagnostic.Location.GetMappedLineSpan();
                    var changePosition = mappedLineSpan.StartLinePosition;

                    var formatMessage = $"{Path.GetRelativePath(workspaceFolder, filePath)}({changePosition.Line + 1},{changePosition.Character + 1}): {message}";

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

        private async Task<Solution> FixDiagnosticsAsync(Solution solution, ImmutableArray<DocumentId> formattableDocuments, ILogger logger, CancellationToken cancellationToken)
        {
            var pairs = _finder.GetAnalyzersAndFixers();
            var paths = formattableDocuments.Select(id => solution.GetDocument(id)?.FilePath)
                .OfType<string>().ToImmutableArray();

            // we need to run each codefix iteratively so ensure that all diagnostics are found and fixed
            foreach (var (analyzer, codefix) in pairs)
            {
                var result = new CodeAnalysisResult();
                await solution.Projects.ForEachAsync(async (project, token) =>
                {
                    await _runner.RunCodeAnalysisAsync(result, analyzer, project, paths, logger, token).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);

                var hasDiagnostics = result.Diagnostics.Any(kvp => kvp.Value.Count > 0);
                if (hasDiagnostics && codefix is object)
                {
                    logger.LogTrace($"Applying fixes for {codefix.GetType().Name}");
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
