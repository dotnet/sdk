// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class InternalRoslynAnalyzerFinder : IAnalyzerFinder
    {
        private readonly static string s_executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static IAnalyzerFinder Instance { get; } = new InternalRoslynAnalyzerFinder();

        private readonly string _featuresCSharpPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CSharp.Features.dll");
        private readonly string _featuresVisualBasicPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CSharp.VisualBasic.dll");

        public ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> GetAnalyzersAndFixers()
        {
            var analyzers = FindAllAnalyzers();

            return analyzers.Select(analyzer => (analyzer, (CodeFixProvider?)null)).ToImmutableArray();
        }

        private ImmutableArray<DiagnosticAnalyzer> FindAllAnalyzers()
        {
            var featuresCSharpReference = new AnalyzerFileReference(_featuresCSharpPath, AssemblyLoader.Instance);
            var csharpAnalyzers = featuresCSharpReference.GetAnalyzers(LanguageNames.CSharp);

            var featuresVisualBasicReference = new AnalyzerFileReference(_featuresVisualBasicPath, AssemblyLoader.Instance);
            var visualBasicAnalyzers = featuresVisualBasicReference.GetAnalyzers(LanguageNames.VisualBasic);

            var allAnalyzers = csharpAnalyzers.Concat(visualBasicAnalyzers).ToImmutableArray();
            return allAnalyzers;
        }

        private ImmutableArray<CodeFixProvider> FindAllCodeFixesAsync()
        {
            return ImmutableArray<CodeFixProvider>.Empty;
        }

        internal class AssemblyLoader : IAnalyzerAssemblyLoader
        {
            public static AssemblyLoader Instance = new AssemblyLoader();

            public void AddDependencyLocation(string fullPath)
            {
            }

            public Assembly LoadFromPath(string fullPath)
            {
                return Assembly.LoadFrom(fullPath);
            }
        }
    }
}
