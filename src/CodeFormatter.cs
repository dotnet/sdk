// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class CodeFormatter
    {
        private static readonly ImmutableArray<ICodeFormatter> s_codeFormatters = new ICodeFormatter[]
        {
            new WhitespaceFormatter(),
            new FinalNewlineFormatter(),
            new EndOfLineFormatter(),
            new CharsetFormatter(),
            new ImportsFormatter(),
            new AnalyzerFormatter("Code Style", new RoslynCodeStyleAnalyzerFinder(), new AnalyzerRunner(), new SolutionCodeFixApplier()),
            new AnalyzerFormatter("Analyzer Reference", new AnalyzerReferenceAnalyzerFinder(), new AnalyzerRunner(), new SolutionCodeFixApplier()),
        }.ToImmutableArray();

        public static async Task<WorkspaceFormatResult> FormatWorkspaceAsync(
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken,
            bool createBinaryLog = false)
        {
            var logWorkspaceWarnings = formatOptions.LogLevel == LogLevel.Trace;

            logger.LogInformation(string.Format(Resources.Formatting_code_files_in_workspace_0, formatOptions.WorkspaceFilePath));

            logger.LogTrace(Resources.Loading_workspace);

            var workspaceStopwatch = Stopwatch.StartNew();

            using var workspace = formatOptions.WorkspaceType == WorkspaceType.Folder
                ? await OpenFolderWorkspaceAsync(formatOptions.WorkspaceFilePath, formatOptions.FileMatcher, cancellationToken).ConfigureAwait(false)
                : await OpenMSBuildWorkspaceAsync(formatOptions.WorkspaceFilePath, formatOptions.WorkspaceType, createBinaryLog, logWorkspaceWarnings, logger, cancellationToken).ConfigureAwait(false);

            if (workspace is null)
            {
                return new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 1);
            }

            var loadWorkspaceMS = workspaceStopwatch.ElapsedMilliseconds;
            logger.LogTrace(Resources.Complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

            var projectPath = formatOptions.WorkspaceType == WorkspaceType.Project ? formatOptions.WorkspaceFilePath : string.Empty;
            var solution = workspace.CurrentSolution;

            logger.LogTrace(Resources.Determining_formattable_files);

            var (fileCount, formatableFiles) = await DetermineFormattableFilesAsync(
                solution, projectPath, formatOptions.FileMatcher, formatOptions.IncludeGeneratedFiles, logger, cancellationToken).ConfigureAwait(false);

            var determineFilesMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS;
            logger.LogTrace(Resources.Complete_in_0_ms, determineFilesMS);

            logger.LogTrace(Resources.Running_formatters);

            var formattedFiles = new List<FormattedFile>();
            var formattedSolution = await RunCodeFormattersAsync(
                solution, formatableFiles, formatOptions, logger, formattedFiles, cancellationToken).ConfigureAwait(false);

            var formatterRanMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS - determineFilesMS;
            logger.LogTrace(Resources.Complete_in_0_ms, formatterRanMS);

            var solutionChanges = formattedSolution.GetChanges(solution);

            var filesFormatted = 0;
            foreach (var projectChanges in solutionChanges.GetProjectChanges())
            {
                foreach (var changedDocumentId in projectChanges.GetChangedDocuments())
                {
                    var changedDocument = solution.GetDocument(changedDocumentId);
                    if (changedDocument?.FilePath is null)
                    {
                        continue;
                    }

                    logger.LogInformation(Resources.Formatted_code_file_0, changedDocument.FilePath);
                    filesFormatted++;
                }
            }

            var exitCode = 0;

            if (formatOptions.SaveFormattedFiles && !workspace.TryApplyChanges(formattedSolution))
            {
                logger.LogError(Resources.Failed_to_save_formatting_changes);
                exitCode = 1;
            }

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(formatOptions.ReportPath))
            {
                var reportFilePath = GetReportFilePath(formatOptions.ReportPath!); // IsNullOrEmpty is not annotated on .NET Core 2.1
                var reportFolderPath = Path.GetDirectoryName(reportFilePath);

                if (!Directory.Exists(reportFolderPath))
                {
                    Directory.CreateDirectory(reportFolderPath);
                }

                logger.LogInformation(Resources.Writing_formatting_report_to_0, reportFilePath);
                var seralizerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var formattedFilesJson = JsonSerializer.Serialize(formattedFiles, seralizerOptions);

                File.WriteAllText(reportFilePath, formattedFilesJson);
            }

            logger.LogDebug(Resources.Formatted_0_of_1_files, filesFormatted, fileCount);

            logger.LogInformation(Resources.Format_complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

            return new WorkspaceFormatResult(filesFormatted, fileCount, exitCode);
        }

        private static string GetReportFilePath(string reportPath)
        {
            var defaultReportName = "format-report.json";
            if (reportPath.EndsWith(".json"))
            {
                return reportPath;
            }
            else if (reportPath == ".")
            {
                return Path.Combine(Environment.CurrentDirectory, defaultReportName);
            }
            else
            {
                return Path.Combine(reportPath, defaultReportName);
            }
        }

        private static async Task<Workspace> OpenFolderWorkspaceAsync(string workspacePath, Matcher fileMatcher, CancellationToken cancellationToken)
        {
            var folderWorkspace = FolderWorkspace.Create();
            await folderWorkspace.OpenFolder(workspacePath, fileMatcher, cancellationToken).ConfigureAwait(false);
            return folderWorkspace;
        }

        private static async Task<Workspace?> OpenMSBuildWorkspaceAsync(
            string solutionOrProjectPath,
            WorkspaceType workspaceType,
            bool createBinaryLog,
            bool logWorkspaceWarnings,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // This property ensures that XAML files will be compiled in the current AppDomain
                // rather than a separate one. Any tasks isolated in AppDomains or tasks that create
                // AppDomains will likely not work due to https://github.com/Microsoft/MSBuildLocator/issues/16.
                { "AlwaysCompileMarkupFilesInSeparateDomain", bool.FalseString },
                // This flag is used at restore time to avoid imports from packages changing the inputs to restore,
                // without this it is possible to get different results between the first and second restore.
                { "ExcludeRestorePackageImports", bool.TrueString },
            };

            var workspace = MSBuildWorkspace.Create(properties);

            Build.Framework.ILogger? binlog = null;
            if (createBinaryLog)
            {
                binlog = new BinaryLogger()
                {
                    Parameters = Path.Combine(Environment.CurrentDirectory, "formatDiagnosticLog.binlog"),
                    Verbosity = Build.Framework.LoggerVerbosity.Diagnostic,
                };
            }

            if (workspaceType == WorkspaceType.Solution)
            {
                await workspace.OpenSolutionAsync(solutionOrProjectPath, msbuildLogger: binlog, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await workspace.OpenProjectAsync(solutionOrProjectPath, msbuildLogger: binlog, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    logger.LogError(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, solutionOrProjectPath);
                    workspace.Dispose();
                    return null;
                }
            }

            LogWorkspaceDiagnostics(logger, logWorkspaceWarnings, workspace.Diagnostics);

            return workspace;

            static void LogWorkspaceDiagnostics(ILogger logger, bool logWorkspaceWarnings, ImmutableList<WorkspaceDiagnostic> diagnostics)
            {
                if (!logWorkspaceWarnings)
                {
                    if (diagnostics.Count > 0)
                    {
                        logger.LogWarning(Resources.Warnings_were_encountered_while_loading_the_workspace_Set_the_verbosity_option_to_the_diagnostic_level_to_log_warnings);
                    }

                    return;
                }

                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                    {
                        logger.LogError(diagnostic.Message);
                    }
                    else
                    {
                        logger.LogWarning(diagnostic.Message);
                    }
                }
            }
        }

        private static async Task<Solution> RunCodeFormattersAsync(
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var codeFormatter in s_codeFormatters)
            {
                formattedSolution = await codeFormatter.FormatAsync(formattedSolution, formattableDocuments, formatOptions, logger, formattedFiles, cancellationToken).ConfigureAwait(false);
            }

            return formattedSolution;
        }

        internal static async Task<(int, ImmutableArray<DocumentId>)> DetermineFormattableFilesAsync(
            Solution solution,
            string projectPath,
            Matcher fileMatcher,
            bool includeGeneratedFiles,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var totalFileCount = solution.Projects.Sum(project => project.DocumentIds.Count);
            var projectFileCount = 0;

            var documentsCoveredByEditorConfig = ImmutableArray.CreateBuilder<DocumentId>(totalFileCount);
            var documentsNotCoveredByEditorConfig = ImmutableArray.CreateBuilder<DocumentId>(totalFileCount);

            var addedFilePaths = new HashSet<string>(totalFileCount);

            foreach (var project in solution.Projects)
            {
                if (project?.FilePath is null)
                {
                    continue;
                }

                // If a project is used as a workspace, then ignore other referenced projects.
                if (!string.IsNullOrEmpty(projectPath) && !project.FilePath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug(Resources.Skipping_referenced_project_0, project.Name);
                    continue;
                }

                // Ignore unsupported project types.
                if (project.Language != LanguageNames.CSharp && project.Language != LanguageNames.VisualBasic)
                {
                    logger.LogWarning(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, project.FilePath);
                    continue;
                }

                projectFileCount += project.DocumentIds.Count;

                foreach (var document in project.Documents)
                {
                    // If we've already added this document, either via a link or multi-targeted framework, then ignore.
                    if (document?.FilePath is null ||
                        addedFilePaths.Contains(document.FilePath))
                    {
                        continue;
                    }

                    addedFilePaths.Add(document.FilePath);

                    if (!fileMatcher.Match(document.FilePath).HasMatches ||
                        !document.SupportsSyntaxTree)
                    {
                        continue;
                    }

                    var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                    if (syntaxTree is null)
                    {
                        throw new Exception($"Unable to get a syntax tree for '{document.Name}'");
                    }

                    if (!includeGeneratedFiles &&
                        await GeneratedCodeUtilities.IsGeneratedCodeAsync(syntaxTree, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    // Track files covered by an editorconfig separately from those not covered.
                    var analyzerConfigOptions = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
                    if (analyzerConfigOptions != null)
                    {
                        documentsCoveredByEditorConfig.Add(document.Id);
                    }
                    else
                    {
                        documentsNotCoveredByEditorConfig.Add(document.Id);
                    }
                }
            }

            // Initially we would format all documents in a workspace, even if some files weren't covered by an
            // .editorconfig and would have defaults applied. This behavior was an early requested change since
            // users were surprised to have files not specified by the .editorconfig modified. The assumption is
            // that users without an .editorconfig still wanted formatting (they did run a formatter after all),
            // so we run on all files with defaults.

            // If no files are covered by an editorconfig, then return them all. Otherwise only return
            // files that are covered by an editorconfig.
            return documentsCoveredByEditorConfig.Count == 0
                ? (projectFileCount, documentsNotCoveredByEditorConfig.ToImmutableArray())
                : (projectFileCount, documentsCoveredByEditorConfig.ToImmutableArray());
        }
    }
}
