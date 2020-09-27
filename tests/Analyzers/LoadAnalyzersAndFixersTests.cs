// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Tools.Analyzers;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    using static AnalyzerAssemblyGenerator;

    public class LoadAnalyzersAndFixersTests
    {
        [Fact]
        public static async Task TestSingleAnalyzerAndFixerAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId"))
            };

            var (analyzers, fixers) = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            var analyzer = Assert.Single(analyzers);
            var fixer = Assert.Single(fixers);
            var analyzerDiagnosticDescriptor = Assert.Single(analyzer.SupportedDiagnostics);
            var fixerDiagnosticId = Assert.Single(fixer.FixableDiagnosticIds);
            Assert.Equal(analyzerDiagnosticDescriptor.Id, fixerDiagnosticId);
        }

        [Fact]
        public static async Task TestMultipleAnalyzersAndFixersAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId1"),
                    GenerateAnalyzerCode("DiagnosticAnalyzer2", "DiagnosticAnalyzerId2"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId1"),
                    GenerateCodeFix("CodeFixProvider2", "DiagnosticAnalyzerId2"))
            };

            var (analyzers, fixers) = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            Assert.Equal(2, analyzers.Length);
            Assert.Equal(2, fixers.Length);
        }

        [Fact]
        public static async Task TestMultipleAnalyzersAndFixersFromTwoAssembliesAsync()
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
            var (analyzers, fixers) = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            Assert.Equal(2, analyzers.Length);
            Assert.Equal(2, fixers.Length);
        }
    }
}
