// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
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

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            var (analyzer, fixer) = Assert.Single(analyzersAndFixers);
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

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            Assert.Equal(2, analyzersAndFixers.Length);
            Assert.Collection(analyzersAndFixers, VerifyAnalyzerCodeFixTuple, VerifyAnalyzerCodeFixTuple);
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
            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            Assert.Equal(2, analyzersAndFixers.Length);
            Assert.Collection(analyzersAndFixers, VerifyAnalyzerCodeFixTuple, VerifyAnalyzerCodeFixTuple);
        }

        [Fact]
        public static async Task NonMatchingIdsAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId"),
                    GenerateCodeFix("CodeFixProvider1", "CodeFixProviderId"))
            };

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            Assert.Empty(analyzersAndFixers);
        }

        [Fact]
        public static async Task SomeMatchingIdsAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId1"),
                    GenerateAnalyzerCode("DiagnosticAnalyzer2", "DiagnosticAnalyzerId2"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId1"),
                    GenerateCodeFix("CodeFixProvider2", "CodeFixProviderId"))
            };

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            var (analyzer, fixer) = Assert.Single(analyzersAndFixers);
            var analyzerDiagnosticDescriptor = Assert.Single(analyzer.SupportedDiagnostics);
            var fixerDiagnosticId = Assert.Single(fixer.FixableDiagnosticIds);
            Assert.Equal(analyzerDiagnosticDescriptor.Id, fixerDiagnosticId);
        }

        [Fact]
        public static async Task SingleIdMapstoMultipleFixersAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId1"),
                    GenerateAnalyzerCode("DiagnosticAnalyzer2", "DiagnosticAnalyzerId1"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId1"),
                    GenerateCodeFix("CodeFixProvider2", "CodeFixProviderId"))
            };

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            Assert.Equal(2, analyzersAndFixers.Length);
            Assert.Collection(analyzersAndFixers, VerifyAnalyzerCodeFixTuple, VerifyAnalyzerCodeFixTuple);
        }

        [Fact]
        public static async Task MultipleIdsMaptoSingleFixerAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId1"),
                    GenerateAnalyzerCode("DiagnosticAnalyzer2", "DiagnosticAnalyzerId1"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId1"))
            };

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            Assert.Equal(2, analyzersAndFixers.Length);
            Assert.Collection(analyzersAndFixers, VerifyAnalyzerCodeFixTuple, VerifyAnalyzerCodeFixTuple);
        }

        private static void VerifyAnalyzerCodeFixTuple((DiagnosticAnalyzer Analyzer, CodeFixProvider Fixer) tuple)
        {
            var analyzerDiagnosticDescriptor = Assert.Single(tuple.Analyzer.SupportedDiagnostics);
            var fixerDiagnosticId = Assert.Single(tuple.Fixer.FixableDiagnosticIds);
            Assert.Equal(analyzerDiagnosticDescriptor.Id, fixerDiagnosticId);
        }
    }
}
