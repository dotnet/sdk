// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Analyzers;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    internal static class AnalyzerOptionsExtensions
    {
        internal const string DotnetDiagnosticPrefix = "dotnet_diagnostic";
        internal const string DotnetAnalyzerDiagnosticPrefix = "dotnet_analyzer_diagnostic";
        internal const string CategoryPrefix = "category";
        internal const string SeveritySuffix = "severity";

        internal const string DotnetAnalyzerDiagnosticSeverityKey = DotnetAnalyzerDiagnosticPrefix + "." + SeveritySuffix;

        internal static string GetDiagnosticIdBasedDotnetAnalyzerDiagnosticSeverityKey(string diagnosticId)
            => $"{DotnetDiagnosticPrefix}.{diagnosticId}.{SeveritySuffix}";
        internal static string GetCategoryBasedDotnetAnalyzerDiagnosticSeverityKey(string category)
            => $"{DotnetAnalyzerDiagnosticPrefix}.{CategoryPrefix}-{category}.{SeveritySuffix}";

        /// <summary>
        /// Tries to get configured severity for the given <paramref name="descriptor"/>
        /// for the given <paramref name="tree"/> from analyzer config options, i.e.
        ///     'dotnet_diagnostic.%descriptor.Id%.severity = %severity%',
        ///     'dotnet_analyzer_diagnostic.category-%RuleCategory%.severity = %severity%',
        ///         or
        ///     'dotnet_analyzer_diagnostic.severity = %severity%'
        /// </summary>
        public static bool TryGetSeverityFromConfiguration(
            this AnalyzerOptions? analyzerOptions,
            SyntaxTree tree,
            Compilation compilation,
            DiagnosticDescriptor descriptor,
            out ReportDiagnostic severity)
        {
            var diagnosticId = descriptor.Id;

            // The unnecessary imports diagnostic and fixer use a special diagnostic id.
            if (diagnosticId == "RemoveUnnecessaryImportsFixable")
            {
                diagnosticId = "IDE0005";
            }

            // If user has explicitly configured severity for this diagnostic ID, that should be respected.
            if (compilation.Options.SpecificDiagnosticOptions.TryGetValue(diagnosticId, out severity))
            {
                return true;
            }

            // If user has explicitly configured severity for this diagnostic ID, that should be respected.
            // For example, 'dotnet_diagnostic.CA1000.severity = error'
            if (compilation.Options.SyntaxTreeOptionsProvider?.TryGetDiagnosticValue(tree, diagnosticId, CancellationToken.None, out severity) == true)
            {
                return true;
            }

            // Analyzer bulk configuration does not apply to:
            //  1. Disabled by default diagnostics
            //  2. Compiler diagnostics
            //  3. Non-configurable diagnostics
            if (analyzerOptions == null ||
                !descriptor.IsEnabledByDefault ||
                descriptor.CustomTags.Any(tag => tag == WellKnownDiagnosticTags.Compiler || tag == WellKnownDiagnosticTags.NotConfigurable))
            {
                severity = default;
                return false;
            }

            var analyzerConfigOptions = analyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);

            // If user has explicitly configured default severity for the diagnostic category, that should be respected.
            // For example, 'dotnet_analyzer_diagnostic.category-security.severity = error'
            var categoryBasedKey = GetCategoryBasedDotnetAnalyzerDiagnosticSeverityKey(descriptor.Category);
            if (analyzerConfigOptions.TryGetValue(categoryBasedKey, out var value) &&
                TryParseSeverity(value, out severity))
            {
                return true;
            }

            // Otherwise, if user has explicitly configured default severity for all analyzer diagnostics, that should be respected.
            // For example, 'dotnet_analyzer_diagnostic.severity = error'
            if (analyzerConfigOptions.TryGetValue(DotnetAnalyzerDiagnosticSeverityKey, out value) &&
                TryParseSeverity(value, out severity))
            {
                return true;
            }

            severity = default;
            return false;
        }

        /// <summary>
        /// Determines whether a diagnostic is configured in the <paramref name="analyzerConfigOptions" />.
        /// </summary>
        public static bool IsDiagnosticSeverityConfigured(this AnalyzerConfigOptions analyzerConfigOptions, Project project, SyntaxTree tree, string diagnosticId, string? diagnosticCategory)
        {
            var optionsProvider = project.CompilationOptions?.SyntaxTreeOptionsProvider;
            return (optionsProvider != null && optionsProvider.TryGetDiagnosticValue(tree, diagnosticId, CancellationToken.None, out _))
                || (diagnosticCategory != null && analyzerConfigOptions.TryGetValue(GetCategoryBasedDotnetAnalyzerDiagnosticSeverityKey(diagnosticCategory), out _))
                || analyzerConfigOptions.TryGetValue(DotnetAnalyzerDiagnosticSeverityKey, out _);
        }

        /// <summary>
        /// Get the configured severity for a diagnostic analyzer from the <paramref name="analyzerConfigOptions" />.
        /// </summary>
        public static DiagnosticSeverity GetDiagnosticSeverity(this AnalyzerConfigOptions analyzerConfigOptions, Project project, SyntaxTree tree, string diagnosticId, string? diagnosticCategory)
        {
            return analyzerConfigOptions.TryGetSeverityFromConfiguration(project, tree, diagnosticId, diagnosticCategory, out var reportSeverity)
                ? reportSeverity.ToSeverity()
                : DiagnosticSeverity.Hidden;
        }

        /// <summary>
        /// Tries to get configured severity for the given <paramref name="diagnosticId"/>
        /// for the given <paramref name="tree"/> from analyzer config options, i.e.
        ///     'dotnet_diagnostic.%descriptor.Id%.severity = %severity%',
        ///     'dotnet_analyzer_diagnostic.category-%RuleCategory%.severity = %severity%',
        ///         or
        ///     'dotnet_analyzer_diagnostic.severity = %severity%'
        /// </summary>
        public static bool TryGetSeverityFromConfiguration(
            this AnalyzerConfigOptions? analyzerConfigOptions,
            Project project,
            SyntaxTree tree,
            string diagnosticId,
            string? diagnosticCategory,
            out ReportDiagnostic severity)
        {
            if (analyzerConfigOptions is null)
            {
                severity = default;
                return false;
            }

            // If user has explicitly configured severity for this diagnostic ID, that should be respected.
            // For example, 'dotnet_diagnostic.CA1000.severity = error'
            var optionsProvider = project.CompilationOptions?.SyntaxTreeOptionsProvider;
            if (optionsProvider != null &&
                optionsProvider.TryGetDiagnosticValue(tree, diagnosticId, CancellationToken.None, out severity))
            {
                return true;
            }

            string? value;
            if (diagnosticCategory != null)
            {
                // If user has explicitly configured default severity for the diagnostic category, that should be respected.
                // For example, 'dotnet_analyzer_diagnostic.category-security.severity = error'
                var categoryBasedKey = GetCategoryBasedDotnetAnalyzerDiagnosticSeverityKey(diagnosticCategory);
                if (analyzerConfigOptions.TryGetValue(categoryBasedKey, out value) &&
                    TryParseSeverity(value, out severity))
                {
                    return true;
                }
            }

            // Otherwise, if user has explicitly configured default severity for all analyzer diagnostics, that should be respected.
            // For example, 'dotnet_analyzer_diagnostic.severity = error'
            if (analyzerConfigOptions.TryGetValue(DotnetAnalyzerDiagnosticSeverityKey, out value) &&
                TryParseSeverity(value, out severity))
            {
                return true;
            }

            severity = default;
            return false;
        }

        internal static bool TryParseSeverity(string value, out ReportDiagnostic severity)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            if (comparer.Equals(value, "default"))
            {
                severity = ReportDiagnostic.Default;
                return true;
            }
            else if (comparer.Equals(value, "error"))
            {
                severity = ReportDiagnostic.Error;
                return true;
            }
            else if (comparer.Equals(value, "warning"))
            {
                severity = ReportDiagnostic.Warn;
                return true;
            }
            else if (comparer.Equals(value, "suggestion"))
            {
                severity = ReportDiagnostic.Info;
                return true;
            }
            else if (comparer.Equals(value, "silent") || comparer.Equals(value, "refactoring"))
            {
                severity = ReportDiagnostic.Hidden;
                return true;
            }
            else if (comparer.Equals(value, "none"))
            {
                severity = ReportDiagnostic.Suppress;
                return true;
            }

            severity = default;
            return false;
        }
    }
}
