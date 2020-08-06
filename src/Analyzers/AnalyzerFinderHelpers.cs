// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal static class AnalyzerFinderHelpers
    {
        public static AnalyzersAndFixers LoadAnalyzersAndFixers(IEnumerable<Assembly> assemblies)
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

            return new AnalyzersAndFixers(diagnosticAnalyzers, codeFixProviders);
        }
    }
}
