// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Analyzers;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    using static AnalyzerAssemblyGenerator;

    [TestClass]
    public class LoadAnalyzersAndFixersTests
    {
        private static AnalyzersAndFixers GetAnalyzersAndFixers(IEnumerable<Assembly> assemblies, string language)
        {
            var analyzers = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
                .Where(type => type.GetCustomAttribute<DiagnosticAnalyzerAttribute>(inherit: false) is { } attribute && attribute.Languages.Contains(language))
                .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type))
                .OfType<DiagnosticAnalyzer>()
                .ToImmutableArray();

            var codeFixes = AnalyzerFinderHelpers.LoadFixers(assemblies, language);
            return new AnalyzersAndFixers(analyzers, codeFixes);
        }

        [TestMethod]
        public async Task TestSingleAnalyzerAndFixerAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId"))
            };

            var (analyzers, fixers) = GetAnalyzersAndFixers(assemblies, LanguageNames.CSharp);
            var analyzer = Assert.ContainsSingle(analyzers);
            var fixer = Assert.ContainsSingle(fixers);
            var analyzerDiagnosticDescriptor = Assert.ContainsSingle(analyzer.SupportedDiagnostics);
            var fixerDiagnosticId = Assert.ContainsSingle(fixer.FixableDiagnosticIds);
            Assert.AreEqual(analyzerDiagnosticDescriptor.Id, fixerDiagnosticId);
        }

        [TestMethod]
        public async Task TestMultipleAnalyzersAndFixersAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId1"),
                    GenerateAnalyzerCode("DiagnosticAnalyzer2", "DiagnosticAnalyzerId2"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId1"),
                    GenerateCodeFix("CodeFixProvider2", "DiagnosticAnalyzerId2"))
            };

            var (analyzers, fixers) = GetAnalyzersAndFixers(assemblies, LanguageNames.CSharp);
            Assert.HasCount(2, analyzers);
            Assert.HasCount(2, fixers);
        }

        [TestMethod]
        public async Task TestMultipleAnalyzersAndFixersFromTwoAssembliesAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId1"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId1")),
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer2", "DiagnosticAnalyzerId2"),
                    GenerateCodeFix("CodeFixProvider2", "DiagnosticAnalyzerId2")),
            };
            var (analyzers, fixers) = GetAnalyzersAndFixers(assemblies, LanguageNames.CSharp);
            Assert.HasCount(2, analyzers);
            Assert.HasCount(2, fixers);
        }
    }
}
