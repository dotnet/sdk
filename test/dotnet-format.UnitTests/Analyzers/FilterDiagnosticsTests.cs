// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
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
            var (_, solution) = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet<string>.Empty;
            var excludeDiagnostics = ImmutableHashSet<string>.Empty;
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                excludeDiagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Single(analyzers);
        }

        [Fact]
        public async Task TestFilterError()
        {
            var (_, solution) = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Error;
            var diagnostics = ImmutableHashSet<string>.Empty;
            var excludeDiagnostics = ImmutableHashSet<string>.Empty;
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                excludeDiagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
        }

        [Fact]
        public async Task TestFilterDiagnostics_NotInDiagnosticsList()
        {
            var (_, solution) = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet.Create("IDE0005");
            var excludeDiagnostics = ImmutableHashSet<string>.Empty;
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                excludeDiagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
        }

        [Fact]
        public async Task TestFilterDiagnostics_InDiagnosticsList()
        {
            var (_, solution) = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet.Create("DiagnosticAnalyzerId");
            var excludeDiagnostics = ImmutableHashSet<string>.Empty;
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                excludeDiagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Single(analyzers);
        }

        [Fact]
        public async Task TestFilterDiagnostics_ExcludedFromDiagnosticsList()
        {
            var (_, solution) = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet<string>.Empty;
            var excludeDiagnostics = ImmutableHashSet.Create("DiagnosticAnalyzerId");
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                excludeDiagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
        }

        [Fact]
        public async Task TestFilterDiagnostics_ExcludeTrumpsInclude()
        {
            var (_, solution) = await GetSolutionAsync();
            var projectAnalyzersAndFixers = await GetProjectAnalyzersAndFixersAsync(solution);
            var project = solution.Projects.First();
            var formattablePaths = ImmutableHashSet.Create(project.Documents.First().FilePath);
            var minimumSeverity = DiagnosticSeverity.Warning;
            var diagnostics = ImmutableHashSet.Create("DiagnosticAnalyzerId");
            var excludeDiagnostics = ImmutableHashSet.Create("DiagnosticAnalyzerId");
            var result = await AnalyzerFormatter.FilterAnalyzersAsync(
                solution,
                projectAnalyzersAndFixers,
                formattablePaths,
                minimumSeverity,
                diagnostics,
                excludeDiagnostics,
                CancellationToken.None);
            var (_, analyzers) = Assert.Single(result);
            Assert.Empty(analyzers);
        }

        private static async Task<AnalyzersAndFixers> GetAnalyzersAndFixersAsync(string language)
        {
            var assemblies = new[]
            {
                await GenerateAssemblyAsync(
                    GenerateAnalyzerCode("DiagnosticAnalyzer1", "DiagnosticAnalyzerId"),
                    GenerateCodeFix("CodeFixProvider1", "DiagnosticAnalyzerId"))
            };

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

        private Task<(Workspace workspace, Solution solution)> GetSolutionAsync()
        {
            var text = SourceText.From("");
            TestState.Sources.Add(text);

            var editorConfig = $@"
root = true
[*.cs]
dotnet_diagnostic.DiagnosticAnalyzerId.severity = warning
";

            return GetSolutionAsync(
                TestState.Sources.ToArray(),
                TestState.AdditionalFiles.ToArray(),
                TestState.AdditionalReferences.ToArray(),
                editorConfig);
        }

        private async Task<ImmutableDictionary<ProjectId, AnalyzersAndFixers>> GetProjectAnalyzersAndFixersAsync(Solution solution)
        {
            var analyzersByLanguage = new Dictionary<string, AnalyzersAndFixers>();
            var builder = ImmutableDictionary.CreateBuilder<ProjectId, AnalyzersAndFixers>();
            foreach (var project in solution.Projects)
            {
                if (!analyzersByLanguage.TryGetValue(project.Language, out var analyzersAndFixers))
                {
                    analyzersAndFixers = await GetAnalyzersAndFixersAsync(project.Language);
                    analyzersByLanguage.Add(project.Language, analyzersAndFixers);
                }

                builder.Add(project.Id, analyzersAndFixers);
            }

            return builder.ToImmutable();
        }

        private protected override ICodeFormatter Formatter { get; }
    }
}
