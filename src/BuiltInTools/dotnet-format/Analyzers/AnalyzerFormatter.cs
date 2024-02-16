// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
        public static AnalyzerFormatter CodeStyleFormatter => new AnalyzerFormatter(
            Resources.Code_Style,
            FixCategory.CodeStyle,
            includeCompilerDiagnostics: false,
            new CodeStyleInformationProvider(),
            new AnalyzerRunner(),
            new SolutionCodeFixApplier());

        public static AnalyzerFormatter ThirdPartyFormatter => new AnalyzerFormatter(
            Resources.Analyzer_Reference,
            FixCategory.Analyzers,
            includeCompilerDiagnostics: true,
            new AnalyzerReferenceInformationProvider(),
            new AnalyzerRunner(),
            new SolutionCodeFixApplier());

        private readonly string _name;
        private readonly bool _includeCompilerDiagnostics;
        private readonly IAnalyzerInformationProvider _informationProvider;
        private readonly IAnalyzerRunner _runner;
        private readonly ICodeFixApplier _applier;

        public FixCategory Category { get; }

        public AnalyzerFormatter(
            string name,
            FixCategory category,
            bool includeCompilerDiagnostics,
            IAnalyzerInformationProvider informationProvider,
            IAnalyzerRunner runner,
            ICodeFixApplier applier)
        {
            _name = name;
            Category = category;
            _includeCompilerDiagnostics = includeCompilerDiagnostics;
            _informationProvider = informationProvider;
            _runner = runner;
            _applier = applier;
        }

        public async Task<Solution> FormatAsync(
            Workspace workspace,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var projectAnalyzersAndFixers = _informationProvider.GetAnalyzersAndFixers(workspace, solution, formatOptions, logger);
            if (projectAnalyzersAndFixers.IsEmpty)
            {
                return solution;
            }

            var allFixers = projectAnalyzersAndFixers.Values.SelectMany(analyzersAndFixers => analyzersAndFixers.Fixers).ToImmutableArray();

            // Only include compiler diagnostics if we have an associated fixer that supports FixAllScope.Solution
            var fixableCompilerDiagnostics = _includeCompilerDiagnostics
                ? allFixers
                    .Where(codefix => codefix.GetFixAllProvider()?.GetSupportedFixAllScopes()?.Contains(FixAllScope.Solution) == true)
                    .SelectMany(codefix => codefix.FixableDiagnosticIds.Where(id => id.StartsWith("CS") || id.StartsWith("BC")))
                    .ToImmutableHashSet()
                : ImmutableHashSet<string>.Empty;

            // Filter compiler diagnostics
            if (!fixableCompilerDiagnostics.IsEmpty && !formatOptions.Diagnostics.IsEmpty)
            {
                fixableCompilerDiagnostics.Intersect(formatOptions.Diagnostics);
            }

            var analysisStopwatch = Stopwatch.StartNew();
            logger.LogTrace(Resources.Running_0_analysis, _name);
            var formattablePaths = await GetFormattablePathsAsync(solution, formattableDocuments, cancellationToken).ConfigureAwait(false);

            logger.LogTrace(Resources.Determining_diagnostics);

            var severity = _informationProvider.GetSeverity(formatOptions);

            // Filter to analyzers that report diagnostics with equal or greater severity.
            var projectAnalyzers = await FilterAnalyzersAsync(solution, projectAnalyzersAndFixers, formattablePaths, severity, formatOptions.Diagnostics, formatOptions.ExcludeDiagnostics, cancellationToken).ConfigureAwait(false);

            // Determine which diagnostics are being reported for each project.
            var projectDiagnostics = await GetProjectDiagnosticsAsync(solution, projectAnalyzers, formattablePaths, formatOptions, severity, fixableCompilerDiagnostics, logger, formattedFiles, cancellationToken).ConfigureAwait(false);

            var projectDiagnosticsMS = analysisStopwatch.ElapsedMilliseconds;
            logger.LogTrace(Resources.Complete_in_0_ms, projectDiagnosticsMS);

            // Only run code fixes when we are saving changes.
            if (formatOptions.SaveFormattedFiles)
            {
                logger.LogTrace(Resources.Fixing_diagnostics);

                // Run each analyzer individually and apply fixes if possible.
                solution = await FixDiagnosticsAsync(solution, projectAnalyzers, allFixers, projectDiagnostics, formattablePaths, formatOptions, severity, fixableCompilerDiagnostics, logger, cancellationToken).ConfigureAwait(false);

                var fixDiagnosticsMS = analysisStopwatch.ElapsedMilliseconds - projectDiagnosticsMS;
                logger.LogTrace(Resources.Complete_in_0_ms, fixDiagnosticsMS);
            }

            logger.LogTrace(Resources.Analysis_complete_in_0ms_, analysisStopwatch.ElapsedMilliseconds);

            return solution;

            async static Task<ImmutableHashSet<string>> GetFormattablePathsAsync(Solution solution, ImmutableArray<DocumentId> formattableDocuments, CancellationToken cancellationToken)
            {
                var formattablePaths = ImmutableHashSet.CreateBuilder<string>();

                foreach (var documentId in formattableDocuments)
                {
                    var document = solution.GetDocument(documentId);
                    if (document is null)
                    {
                        document = await solution.GetSourceGeneratedDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);

                        if (document is null)
                        {
                            continue;
                        }
                    }

                    formattablePaths.Add(document.FilePath!);
                }

                return formattablePaths.ToImmutable();
            }
        }

        private async Task<ImmutableDictionary<ProjectId, ImmutableHashSet<string>>> GetProjectDiagnosticsAsync(
            Solution solution,
            ImmutableDictionary<ProjectId, ImmutableArray<DiagnosticAnalyzer>> projectAnalyzers,
            ImmutableHashSet<string> formattablePaths,
            FormatOptions options,
            DiagnosticSeverity severity,
            ImmutableHashSet<string> fixableCompilerDiagnostics,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var result = new CodeAnalysisResult(options.Diagnostics, options.ExcludeDiagnostics);
            var projects = options.WorkspaceType == WorkspaceType.Solution
                ? solution.Projects
                : solution.Projects.Where(project => project.FilePath == options.WorkspaceFilePath);
            foreach (var project in projects)
            {
                var analyzers = projectAnalyzers[project.Id];
                if (analyzers.IsEmpty)
                {
                    continue;
                }

                // Run all the filtered analyzers to determine which are reporting diagnostic.
                await _runner.RunCodeAnalysisAsync(result, analyzers, project, formattablePaths, severity, fixableCompilerDiagnostics, logger, cancellationToken).ConfigureAwait(false);
            }

            LogDiagnosticLocations(solution, result.Diagnostics.SelectMany(kvp => kvp.Value), options.SaveFormattedFiles, options.ChangesAreErrors, logger, options.LogLevel, formattedFiles);

            return result.Diagnostics.ToImmutableDictionary(kvp => kvp.Key.Id, kvp => kvp.Value.Select(diagnostic => diagnostic.Id).ToImmutableHashSet());

            static void LogDiagnosticLocations(Solution solution, IEnumerable<Diagnostic> diagnostics, bool saveFormattedFiles, bool changesAreErrors, ILogger logger, LogLevel logLevel, List<FormattedFile> formattedFiles)
            {
                foreach (var diagnostic in diagnostics)
                {
                    var document = solution.GetDocument(diagnostic.Location.SourceTree);
                    if (document is null)
                    {
                        continue;
                    }

                    var mappedLineSpan = diagnostic.Location.GetMappedLineSpan();
                    var diagnosticPosition = mappedLineSpan.StartLinePosition;

                    if (!saveFormattedFiles || logLevel == LogLevel.Debug)
                    {
                        logger.LogDiagnosticIssue(document, diagnosticPosition, diagnostic, changesAreErrors);
                    }

                    formattedFiles.Add(new FormattedFile(document, new[] { new FileChange(diagnosticPosition, diagnostic.Id, $"{diagnostic.Severity.ToString().ToLower()} {diagnostic.Id}: {diagnostic.GetMessage()}") }));
                }
            }
        }

        private async Task<Solution> FixDiagnosticsAsync(
            Solution solution,
            ImmutableDictionary<ProjectId, ImmutableArray<DiagnosticAnalyzer>> projectAnalyzers,
            ImmutableArray<CodeFixProvider> allFixers,
            ImmutableDictionary<ProjectId, ImmutableHashSet<string>> projectDiagnostics,
            ImmutableHashSet<string> formattablePaths,
            FormatOptions options,
            DiagnosticSeverity severity,
            ImmutableHashSet<string> fixableCompilerDiagnostics,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // Determine the reported diagnostic ids
            var reportedDiagnostics = projectDiagnostics.SelectMany(kvp => kvp.Value).Distinct().ToImmutableArray();
            if (reportedDiagnostics.IsEmpty)
            {
                return solution;
            }

            var fixersById = CreateFixerMap(reportedDiagnostics, allFixers);

            // We need to run each codefix iteratively so ensure that all diagnostics are found and fixed.
            foreach (var diagnosticId in reportedDiagnostics)
            {
                var codefixes = fixersById[diagnosticId];

                // If there is no codefix, there is no reason to run analysis again.
                if (codefixes.IsEmpty)
                {
                    logger.LogWarning(Resources.Unable_to_fix_0_No_associated_code_fix_found, diagnosticId);
                    continue;
                }

                var result = new CodeAnalysisResult(options.Diagnostics, options.ExcludeDiagnostics);
                foreach (var project in solution.Projects)
                {
                    // Only run analysis on projects that had previously reported the diagnostic
                    if (!projectDiagnostics.TryGetValue(project.Id, out var diagnosticIds)
                        || !diagnosticIds.Contains(diagnosticId))
                    {
                        continue;
                    }

                    var analyzers = projectAnalyzers[project.Id]
                        .Where(analyzer => analyzer.SupportedDiagnostics.Any(descriptor => descriptor.Id == diagnosticId))
                        .ToImmutableArray();
                    await _runner.RunCodeAnalysisAsync(result, analyzers, project, formattablePaths, severity, fixableCompilerDiagnostics, logger, cancellationToken).ConfigureAwait(false);
                }

                var hasDiagnostics = result.Diagnostics.Any(kvp => kvp.Value.Count > 0);
                if (hasDiagnostics)
                {
                    foreach (var codefix in codefixes)
                    {
                        var changedSolution = await _applier.ApplyCodeFixesAsync(solution, result, codefix, diagnosticId, logger, cancellationToken).ConfigureAwait(false);
                        if (changedSolution.GetChanges(solution).Any())
                        {
                            solution = changedSolution;
                        }
                    }
                }
            }

            return solution;

            static ImmutableDictionary<string, ImmutableArray<CodeFixProvider>> CreateFixerMap(
                ImmutableArray<string> diagnosticIds,
                ImmutableArray<CodeFixProvider> fixers)
            {
                return diagnosticIds.ToImmutableDictionary(
                    id => id,
                    id => fixers
                        .Where(fixer => ContainsFixableId(fixer, id))
                        .ToImmutableArray());
            }

            static bool ContainsFixableId(CodeFixProvider fixer, string id)
            {
                // The unnecessary imports diagnostic and fixer use a special diagnostic id.
                if (id == "IDE0005" && fixer.FixableDiagnosticIds.Contains("RemoveUnnecessaryImportsFixable"))
                {
                    return true;
                }

                return fixer.FixableDiagnosticIds.Contains(id);
            }
        }

        internal static async Task<ImmutableDictionary<ProjectId, ImmutableArray<DiagnosticAnalyzer>>> FilterAnalyzersAsync(
            Solution solution,
            ImmutableDictionary<ProjectId, AnalyzersAndFixers> projectAnalyzersAndFixers,
            ImmutableHashSet<string> formattablePaths,
            DiagnosticSeverity minimumSeverity,
            ImmutableHashSet<string> diagnostics,
            ImmutableHashSet<string> excludeDiagnostics,
            CancellationToken cancellationToken)
        {
            // We only want to run analyzers for each project that have the potential for reporting a diagnostic with
            // a severity equal to or greater than specified.
            var projectAnalyzers = ImmutableDictionary.CreateBuilder<ProjectId, ImmutableArray<DiagnosticAnalyzer>>();
            foreach (var projectId in projectAnalyzersAndFixers.Keys)
            {
                var project = solution.GetProject(projectId);
                if (project is null)
                {
                    continue;
                }

                // Skip if the project does not contain any of the formattable paths.
                if (!project.Documents.Any(d => d.FilePath is not null && formattablePaths.Contains(d.FilePath)))
                {
                    projectAnalyzers.Add(projectId, ImmutableArray<DiagnosticAnalyzer>.Empty);
                    continue;
                }

                var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

                // Filter analyzers by project's language
                var filteredAnalyzer = projectAnalyzersAndFixers[projectId].Analyzers
                    .Where(analyzer => DoesAnalyzerSupportLanguage(analyzer, project.Language));
                foreach (var analyzer in filteredAnalyzer)
                {
                    // Filter by excluded diagnostics
                    if (!excludeDiagnostics.IsEmpty &&
                        analyzer.SupportedDiagnostics.All(descriptor => excludeDiagnostics.Contains(descriptor.Id)))
                    {
                        continue;
                    }

                    // Filter by diagnostics
                    if (!diagnostics.IsEmpty &&
                        !analyzer.SupportedDiagnostics.Any(descriptor => diagnostics.Contains(descriptor.Id)))
                    {
                        continue;
                    }

                    // Always run naming style analyzers because we cannot determine potential severity.
                    // The reported diagnostics will be filtered by severity when they are run.
                    if (analyzer.GetType().FullName?.EndsWith("NamingStyleDiagnosticAnalyzer") == true)
                    {
                        analyzers.Add(analyzer);
                        continue;
                    }

                    var severity = await analyzer.GetSeverityAsync(project, formattablePaths, cancellationToken).ConfigureAwait(false);
                    if (severity >= minimumSeverity)
                    {
                        analyzers.Add(analyzer);
                    }
                }

                projectAnalyzers.Add(projectId, analyzers.ToImmutableArray());
            }

            return projectAnalyzers.ToImmutableDictionary();
        }

        private static bool DoesAnalyzerSupportLanguage(DiagnosticAnalyzer analyzer, string language)
        {
            return analyzer.GetType()
                .GetCustomAttributes(typeof(DiagnosticAnalyzerAttribute), true)
                .OfType<DiagnosticAnalyzerAttribute>()
                .Any(attribute => attribute.Languages.Contains(language));
        }
    }
}
