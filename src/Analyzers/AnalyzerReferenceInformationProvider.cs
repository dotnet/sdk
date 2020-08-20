// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerReferenceInformationProvider : IAnalyzerInformationProvider
    {
        private static readonly string[] s_roslynCodeStyleAssmeblies = new[]
        {
            "Microsoft.CodeAnalysis.CodeStyle",
            "Microsoft.CodeAnalysis.CodeStyle.Fixes",
            "Microsoft.CodeAnalysis.CSharp.CodeStyle",
            "Microsoft.CodeAnalysis.CSharp.CodeStyle.Fixes",
            "Microsoft.CodeAnalysis.VisualBasic.CodeStyle",
            "Microsoft.CodeAnalysis.VisualBasic.CodeStyle.Fixes"
        };

        public (ImmutableArray<DiagnosticAnalyzer> Analyzers, ImmutableArray<CodeFixProvider> Fixers) GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            if (!formatOptions.FixAnalyzers)
            {
                return (ImmutableArray<DiagnosticAnalyzer>.Empty, ImmutableArray<CodeFixProvider>.Empty);
            }

            var assemblies = solution.Projects
                .SelectMany(project => project.AnalyzerReferences.Select(reference => reference.FullPath))
                .Distinct()
                .Select(TryLoadAssemblyFrom)
                .OfType<Assembly>()
                .ToImmutableArray();

            return AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
        }

        private Assembly? TryLoadAssemblyFrom(string? path)
        {
            // Since we are not deploying these assemblies we need to ensure the files exist.
            if (path is null || !File.Exists(path))
            {
                return null;
            }

            // Roslyn CodeStyle analysis is handled with the --fix-style option.
            var assemblyFileName = Path.GetFileNameWithoutExtension(path);
            if (s_roslynCodeStyleAssmeblies.Contains(assemblyFileName))
            {
                return null;
            }

            try
            {
                // First try loading the assembly from disk.
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }
            catch { }

            try
            {
                // Next see if this assembly has already been loaded into our context.
                var assemblyName = AssemblyLoadContext.GetAssemblyName(path);
                if (assemblyName?.Name != null)
                {
                    return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName.Name));
                }
            }
            catch { }

            // Give up.
            return null;
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.AnalyzerSeverity;
    }
}
