// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class RoslynCodeStyleAnalyzerFinder : IAnalyzerFinder
    {
        private readonly static string s_executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly string _featuresCSharpPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CSharp.Features.dll");
        private readonly string _featuresVisualBasicPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.VisualBasic.Features.dll");

        public ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> GetAnalyzersAndFixers()
        {
            var assemblies = new[]
            {
                _featuresCSharpPath,
                _featuresVisualBasicPath
            }.Select(path => Assembly.LoadFile(path));

            // This is borrowed from omnisharp.
            // see https://github.com/OmniSharp/omnisharp-roslyn/blob/62b3b52d01251fdc0564a600010936e677f24a2e/src/OmniSharp.Roslyn/Services/AbstractCodeActionProvider.cs#L26-L49
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
    }
}
