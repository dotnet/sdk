// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;

using BenchmarkDotNet.Attributes;

using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    public class NoFilesFormatted
    {
        private const string FormattedProjectPath = "tests/projects/for_code_formatter/formatted_project/";
        private const string FormattedProjectFilePath = FormattedProjectPath + "formatted_project.csproj";
        private const string FormattedSolutionFilePath = "tests/projects/for_code_formatter/formatted_solution/formatted_solution.sln";

        private static EmptyLogger EmptyLogger => new EmptyLogger();
        private static SourceFileMatcher AllFileMatcher => SourceFileMatcher.CreateMatcher(Array.Empty<string>(), Array.Empty<string>());

        [IterationSetup]
        public void NoFilesFormattedSetup()
        {
            SolutionPathSetter.SetCurrentDirectory();
            MSBuildRegister.RegisterInstance(Environment.CurrentDirectory);
        }

        [Benchmark(Description = "No Files are Formatted (folder)")]
        public void NoFilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(FormattedProjectPath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                NoRestore: false,
                LogLevel.Error,
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                Diagnostics: ImmutableHashSet<string>.Empty,
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false,
                BinaryLogPath: null);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "No Files are Formatted (project)")]
        public void NoFilesFormattedProject()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(FormattedProjectFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                NoRestore: false,
                LogLevel.Error,
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                Diagnostics: ImmutableHashSet<string>.Empty,
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false,
                BinaryLogPath: null);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "No Files are Formatted (solution)")]
        public void NoFilesFormattedSolution()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(FormattedSolutionFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                NoRestore: false,
                LogLevel.Error,
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                Diagnostics: ImmutableHashSet<string>.Empty,
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false,
                BinaryLogPath: null);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [IterationCleanup]
        public void NoFilesFormattedCleanup() => SolutionPathSetter.UnsetCurrentDirectory();
    }
}
