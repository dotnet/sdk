using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
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
            var result = await AnalyzerFinderHelpers.FilterBySeverityAsync(projects,
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
            var result = await AnalyzerFinderHelpers.FilterBySeverityAsync(projects,
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

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            return ImmutableArray.Create(analyzersAndFixers[0].Analyzer);
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
