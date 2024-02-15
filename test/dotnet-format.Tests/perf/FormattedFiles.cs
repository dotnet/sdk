// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;

using BenchmarkDotNet.Attributes;

using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf.Micro
{
    public class FormattedFiles
    {
        private const string UnformattedProjectPath = "tests/projects/for_code_formatter/unformatted_project/";
        private const string UnformattedProjectFilePath = UnformattedProjectPath + "unformatted_project.csproj";
        private const string UnformattedSolutionFilePath = "tests/projects/for_code_formatter/unformatted_solution/unformatted_solution.sln";

        private static EmptyLogger EmptyLogger => new EmptyLogger();
        private static SourceFileMatcher AllFileMatcher => SourceFileMatcher.CreateMatcher(Array.Empty<string>(), Array.Empty<string>());

        [IterationSetup]
        public void NoFilesFormattedSetup()
        {
            SolutionPathSetter.SetCurrentDirectory();
            MSBuildRegister.RegisterInstance(Environment.CurrentDirectory);
        }

        [Benchmark(Description = "Whitespace Formatting (folder)")]
        public void FilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedProjectPath);
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

        [Benchmark(Description = "Whitespace Formatting (project)")]
        public void FilesFormattedProject()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedProjectFilePath);
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

        [Benchmark(Description = "Whitespace Formatting (solution)")]
        public void FilesFormattedSolution()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedSolutionFilePath);
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
