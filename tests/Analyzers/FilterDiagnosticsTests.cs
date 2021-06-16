// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            var solution = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet<string>.Empty;
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Single(analyzers);
        }

        [Fact]
        public async Task TestFilterError()
        {
            var solution = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Error;
            var diagnostics = ImmutableHashSet<string>.Empty;
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
        }

        [Fact]
        public async Task TestFilterDiagnostics_NotInDiagnosticsList()
        {
            var solution = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet.Create("IDE0005");
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
        }

        [Fact]
        public async Task TestFilterDiagnostics_InDiagnosticsList()
        {
            var solution = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet.Create("DiagnosticAnalyzerId");
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Single(analyzers);
        }

        private static async Task<AnalyzersAndFixers> GetAnalyzersAndFixersAsync()
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId"))
            };

            return AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
        }

        private Task<Solution> GetSolutionAsync()
        {
            var text = SourceText.From("");
            TestState.Sources.Add(text);

            return GetSolutionAsync(
                TestState.Sources.ToArray(),
                TestState.AdditionalFiles.ToArray(),
                TestState.AdditionalReferences.ToArray(),
                "root = true");
        }

        private async Task<ImmutableDictionary<ProjectId, AnalyzersAndFixers>> GetProjectAnalyzersAndFixersAsync(Solution solution)
        {
            var analyzersAndFixers = await GetAnalyzersAndFixersAsync();

            return solution.Projects
                .ToImmutableDictionary(project => project.Id, project => analyzersAndFixers);
        }

        private protected override ICodeFormatter Formatter { get; }
    }
}
