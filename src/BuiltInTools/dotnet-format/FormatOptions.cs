// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal record FormatOptions(
            string WorkspaceFilePath,
            WorkspaceType WorkspaceType,
            bool NoRestore,
            LogLevel LogLevel,
            FixCategory FixCategory,
            DiagnosticSeverity CodeStyleSeverity,
            DiagnosticSeverity AnalyzerSeverity,
            ImmutableHashSet<string> Diagnostics,
            ImmutableHashSet<string> ExcludeDiagnostics,
            bool SaveFormattedFiles,
            bool ChangesAreErrors,
            SourceFileMatcher FileMatcher,
            string? ReportPath,
            string? BinaryLogPath,
            bool IncludeGeneratedFiles)
    {
        public static FormatOptions Instance = new(
            WorkspaceFilePath: null!, // must be supplied
            WorkspaceType: default, // must be supplied
            NoRestore: false,
            LogLevel: LogLevel.Warning,
            FixCategory: default, // must be supplied
            CodeStyleSeverity: DiagnosticSeverity.Warning,
            AnalyzerSeverity: DiagnosticSeverity.Warning,
            Diagnostics: ImmutableHashSet<string>.Empty,
            ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
            SaveFormattedFiles: true,
            ChangesAreErrors: false,
            FileMatcher: SourceFileMatcher.CreateMatcher(Array.Empty<string>(), Array.Empty<string>()),
            ReportPath: null,
            BinaryLogPath: null,
            IncludeGeneratedFiles: false);
    }
}
