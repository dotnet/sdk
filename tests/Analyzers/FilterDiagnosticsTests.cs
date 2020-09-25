// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    using static AnalyzerAssemblyGenerator;

    public class FilterDiagnosticsTests : CSharpFormatterTests
    {
        public FilterDiagnosticsTests()
        {
            MSBuildRegistrar.RegisterInstance();
        }

        [Fact]
        public async Task TestFilterWarning()
        {
            var solution = GetSolution();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var result = await AnalyzerFormatter.FilterBySeverityAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Single(analyzers);
        }

        [Fact]
        public async Task TestFilterError()
        {
            var solution = GetSolution();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Error;
            var result = await AnalyzerFormatter.FilterBySeverityAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
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

        private Solution GetSolution()
        {
            var text = SourceText.From("");
            TestState.Sources.Add(text);

            return GetSolution(
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
