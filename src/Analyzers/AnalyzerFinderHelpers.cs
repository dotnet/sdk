// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal static class AnalyzerFinderHelpers
    {
        public static ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> LoadAnalyzersAndFixers(
            IEnumerable<Assembly> assemblies,
            ILogger logger)
        {
            var types = assemblies
                .SelectMany(assembly => assembly.GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface &&
                            !type.GetTypeInfo().IsAbstract &&
                            !type.GetTypeInfo().ContainsGenericParameters));

            var codeFixProviders = types
                .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
                .Select(type => type.TryCreateInstance<CodeFixProvider>(out var instance) ? instance : null)
                .OfType<CodeFixProvider>()
                .ToImmutableArray();

            var diagnosticAnalyzers = types
                .Where(t => typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
                .Select(type => type.TryCreateInstance<DiagnosticAnalyzer>(out var instance) ? instance : null)
                .OfType<DiagnosticAnalyzer>()
                .ToImmutableArray();

            var builder = ImmutableArray.CreateBuilder<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)>();
            foreach (var diagnosticAnalyzer in diagnosticAnalyzers)
            {
                var diagnosticIds = diagnosticAnalyzer.SupportedDiagnostics.Select(diagnostic => diagnostic.Id).ToImmutableHashSet();
                var codeFixProvider = codeFixProviders.FirstOrDefault(codeFixProvider => codeFixProvider.FixableDiagnosticIds.Any(id => diagnosticIds.Contains(id)));

                if (codeFixProvider is null)
                {
                    continue;
                }

                builder.Add((diagnosticAnalyzer, codeFixProvider));
            }

            return builder.ToImmutableArray();
        }

        public static async Task<ImmutableDictionary<Project, ImmutableArray<DiagnosticAnalyzer>>> FilterBySeverityAsync(
            IEnumerable<Project> projects,
            ImmutableArray<DiagnosticAnalyzer> allAnalyzers,
            ImmutableHashSet<string> formattablePaths,
            DiagnosticSeverity minimumSeverity,
            CancellationToken cancellationToken)
        {
            var projectAnalyzers = ImmutableDictionary.CreateBuilder<Project, ImmutableArray<DiagnosticAnalyzer>>();
            foreach (var project in projects)
            {
                var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

                foreach (var analyzer in allAnalyzers)
                {
                    var severity = await analyzer.GetSeverityAsync(project, formattablePaths, cancellationToken).ConfigureAwait(false);
                    if (severity >= minimumSeverity)
                    {
                        analyzers.Add(analyzer);
                    }
                }

                projectAnalyzers.Add(project, analyzers.ToImmutableArray());
            }

            return projectAnalyzers.ToImmutableDictionary();
        }
    }
}
