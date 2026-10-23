// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            bool IncludeGeneratedFiles,
            string? TargetFramework)
    {
        /// <summary>
        /// Disables the on-disk formatting cache. This is a record body property (not a positional
        /// parameter) so the positional constructor used by tests is unaffected.
        /// </summary>
        public bool NoCache { get; init; } = false;

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
            IncludeGeneratedFiles: false,
            TargetFramework: null);
    }
}
