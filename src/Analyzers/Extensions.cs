// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    public static class Extensions
    {
        private static Assembly MicrosoftCodeAnalysisFeaturesAssembly { get; }
        private static Type IDEDiagnosticIdToOptionMappingHelperType { get; }
        private static MethodInfo TryGetMappedOptionsMethod { get; }

        static Extensions()
        {
            MicrosoftCodeAnalysisFeaturesAssembly = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Features"));
            IDEDiagnosticIdToOptionMappingHelperType = MicrosoftCodeAnalysisFeaturesAssembly.GetType("Microsoft.CodeAnalysis.Diagnostics.IDEDiagnosticIdToOptionMappingHelper")!;
            TryGetMappedOptionsMethod = IDEDiagnosticIdToOptionMappingHelperType.GetMethod("TryGetMappedOptions", BindingFlags.Static | BindingFlags.Public)!;
        }

        public static bool Any(this SolutionChanges solutionChanges)
                => solutionChanges.GetProjectChanges()
                    .Any(x => x.GetChangedDocuments().Any() || x.GetChangedAdditionalDocuments().Any());

        /// <summary>
        /// Get the highest possible severity for any formattable document in the project.
        /// </summary>
        public static async Task<DiagnosticSeverity> GetSeverityAsync(
            this DiagnosticAnalyzer analyzer,
            Project project,
            ImmutableHashSet<string> formattablePaths,
            CancellationToken cancellationToken)
        {
            var severity = DiagnosticSeverity.Hidden;
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                return severity;
            }

            foreach (var document in project.Documents)
            {
                // Is the document formattable?
                if (document.FilePath is null || !formattablePaths.Contains(document.FilePath))
                {
                    continue;
                }

                var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

                var documentSeverity = analyzer.GetSeverity(document, project.AnalyzerOptions, options, compilation);
                if (documentSeverity > severity)
                {
                    severity = documentSeverity;
                }
            }

            return severity;
        }

        public static DiagnosticSeverity ToSeverity(this ReportDiagnostic reportDiagnostic)
        {
            return reportDiagnostic switch
            {
                ReportDiagnostic.Error => DiagnosticSeverity.Error,
                ReportDiagnostic.Warn => DiagnosticSeverity.Warning,
                ReportDiagnostic.Info => DiagnosticSeverity.Info,
                _ => DiagnosticSeverity.Hidden
            };
        }

        private static DiagnosticSeverity GetSeverity(
            this DiagnosticAnalyzer analyzer,
            Document document,
            AnalyzerOptions? analyzerOptions,
            OptionSet options,
            Compilation compilation)
        {
            var severity = DiagnosticSeverity.Hidden;

            if (!document.TryGetSyntaxTree(out var tree))
            {
                return severity;
            }

            foreach (var descriptor in analyzer.SupportedDiagnostics)
            {
                if (severity == DiagnosticSeverity.Error)
                {
                    break;
                }

                if (analyzerOptions.TryGetSeverityFromConfiguration(tree, compilation, descriptor, out var reportDiagnostic))
                {
                    var configuredSeverity = reportDiagnostic.ToSeverity();
                    if (configuredSeverity > severity)
                    {
                        severity = configuredSeverity;
                    }

                    continue;
                }

                if (TryGetSeverityFromCodeStyleOption(descriptor, compilation, options, out var codeStyleSeverity))
                {
                    if (codeStyleSeverity > severity)
                    {
                        severity = codeStyleSeverity;
                    }

                    continue;
                }

                if (descriptor.DefaultSeverity > severity)
                {
                    severity = descriptor.DefaultSeverity;
                }
            }

            return severity;

            static bool TryGetSeverityFromCodeStyleOption(
                DiagnosticDescriptor descriptor,
                Compilation compilation,
                OptionSet options,
                out DiagnosticSeverity severity)
            {
                severity = DiagnosticSeverity.Hidden;

                var parameters = new object?[] { descriptor.Id, compilation.Language, null };
                var result = (bool)(TryGetMappedOptionsMethod.Invoke(null, parameters) ?? false);

                if (!result)
                {
                    return false;
                }

                var codeStyleOptions = (IEnumerable)parameters[2]!;
                foreach (var codeStyleOptionObj in codeStyleOptions)
                {
                    var codeStyleOption = (IOption)codeStyleOptionObj!;
                    var option = options.GetOption(new OptionKey(codeStyleOption, codeStyleOption.IsPerLanguage ? compilation.Language : null));
                    if (option is null)
                    {
                        continue;
                    }

                    var notificationProperty = option.GetType().GetProperty("Notification");
                    if (notificationProperty is null)
                    {
                        continue;
                    }

                    var notification = notificationProperty.GetValue(option);
                    var reportDiagnosticValue = notification?.GetType().GetProperty("Severity")?.GetValue(notification);
                    if (reportDiagnosticValue is null)
                    {
                        continue;
                    }

                    var codeStyleSeverity = ToSeverity((ReportDiagnostic)reportDiagnosticValue);
                    if (codeStyleSeverity > severity)
                    {
                        severity = codeStyleSeverity;
                    }
                }

                return true;
            }
        }
    }
}
