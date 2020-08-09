// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Formatters;

using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    using static AnalyzerAssemblyGenerator;

    public class FilterDiagnosticsTests : CSharpFormatterTests
    {
        [Fact]
        public async Task TestFilterWarning()
        {
            var projects = GetProjects();
            var allAnalyzers = await GetAnalyzersAsync();
            var formattablePaths = ImmutableHashSet.Create(projects.First().Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var result = await AnalyzerFormatter.FilterBySeverityAsync(
                projects,
                allAnalyzers,
                formattablePaths,
                minimumSeverity,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Single(analyzers);
        }

        [Fact]
        public async Task TestFilterError()
        {
            var projects = GetProjects();
            var allAnalyzers = await GetAnalyzersAsync();
            var formattablePaths = ImmutableHashSet.Create(projects.First().Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Error;
            var result = await AnalyzerFormatter.FilterBySeverityAsync(
                projects,
                allAnalyzers,
                formattablePaths,
                minimumSeverity,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
        }

        private async Task<ImmutableArray<DiagnosticAnalyzer>> GetAnalyzersAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId"))
            };

            var (analyzers, _) = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            return analyzers;
        }

        private IEnumerable<Project> GetProjects()
        {
            var text = SourceText.From("");
            TestState.Sources.Add(text);

            var solution = GetSolution(TestState.Sources.ToArray(),
                                       TestState.AdditionalFiles.ToArray(),
                                       TestState.AdditionalReferences.ToArray(),
                                       "root = true");
            return solution.Projects;
        }

        private protected override ICodeFormatter Formatter { get; }
    }
}
