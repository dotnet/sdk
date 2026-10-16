// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    [TestClass]
    public class AnalyzerRunnerTests
    {
        private const string UnusedVariableDiagnosticId = "CS0168";

        private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        [TestMethod]
        public async Task RunCodeAnalysisAsync_DenseProjectReferenceGraph_CompletesPromptly()
        {
            // Each project references every earlier one, so an unmemoized walk of the references
            // visits the first project 2^38 times and never finishes.
            using var workspace = new AdhocWorkspace();
            var solution = CreateGraph(workspace.CurrentSolution, projectCount: 40, brokenProjectIndex: -1, chain: false, out var topProject);

            var (result, logger) = await RunWithTimeoutAsync(solution.GetProject(topProject), TimeSpan.FromSeconds(60));

            var diagnostic = Assert.ContainsSingle(Assert.ContainsSingle(result.Diagnostics).Value);
            Assert.AreEqual(UnusedVariableDiagnosticId, diagnostic.Id);
            Assert.IsEmpty(logger.GetLog());
        }

        [TestMethod]
        public async Task RunCodeAnalysisAsync_SharedReferenceMissingCorlib_SkipsProject()
        {
            // Break a shared dependency in the middle of the graph (referenced by Proj8 and Proj9), so the
            // failure has to be found among projects that load successfully.
            using var workspace = new AdhocWorkspace();
            var solution = CreateGraph(workspace.CurrentSolution, projectCount: 10, brokenProjectIndex: 7, chain: false, out var topProject);

            var (result, logger) = await RunWithTimeoutAsync(solution.GetProject(topProject), TimeSpan.FromSeconds(60));

            Assert.IsEmpty(result.Diagnostics);
            Assert.Contains(
                string.Format(Resources.Required_references_did_not_load_for_0_or_referenced_project_Run_dotnet_restore_prior_to_formatting, "Proj9"),
                logger.GetLog());
        }

        [TestMethod]
        public async Task RunCodeAnalysisAsync_IndirectReferenceMissingCorlib_SkipsProject()
        {
            // Each project references only its predecessor, so Proj0 is reachable from Proj2 only transitively.
            using var workspace = new AdhocWorkspace();
            var solution = CreateGraph(workspace.CurrentSolution, projectCount: 3, brokenProjectIndex: 0, chain: true, out var topProject);

            var (result, logger) = await RunWithTimeoutAsync(solution.GetProject(topProject), TimeSpan.FromSeconds(60));

            Assert.IsEmpty(result.Diagnostics);
            Assert.Contains(
                string.Format(Resources.Required_references_did_not_load_for_0_or_referenced_project_Run_dotnet_restore_prior_to_formatting, "Proj2"),
                logger.GetLog());
        }

        private static Solution CreateGraph(Solution solution, int projectCount, int brokenProjectIndex, bool chain, out ProjectId topProject)
        {
            var projectIds = new List<ProjectId>();
            for (var i = 0; i < projectCount; i++)
            {
                var projectId = ProjectId.CreateNewId();
                var name = $"Proj{i}";
                solution = solution.AddProject(ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    name,
                    name,
                    LanguageNames.CSharp,
                    compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                    metadataReferences: i == brokenProjectIndex ? [] : [CorlibReference],
                    projectReferences: (chain ? projectIds.TakeLast(1) : projectIds).Select(id => new ProjectReference(id))));

                var documentId = DocumentId.CreateNewId(projectId);
                solution = solution.AddDocument(
                    documentId,
                    $"Class{i}.cs",
                    SourceText.From($"public class Class{i} {{ void M() {{ int unused; }} }}"),
                    filePath: $"/{name}/Class{i}.cs");

                projectIds.Add(projectId);
            }

            topProject = projectIds[^1];
            return solution;
        }

        private static async Task<(CodeAnalysisResult Result, TestLogger Logger)> RunWithTimeoutAsync(Project project, TimeSpan timeout)
        {
            var result = new CodeAnalysisResult(ImmutableHashSet<string>.Empty, ImmutableHashSet<string>.Empty);
            var logger = new TestLogger();
            using var cancellationTokenSource = new CancellationTokenSource(timeout * 2);

            var analysis = new AnalyzerRunner().RunCodeAnalysisAsync(
                result,
                ImmutableArray<DiagnosticAnalyzer>.Empty,
                project,
                ImmutableHashSet.Create(project.Documents.Single().FilePath),
                DiagnosticSeverity.Warning,
                ImmutableHashSet.Create(UnusedVariableDiagnosticId),
                logger,
                cancellationTokenSource.Token);

            var completed = await Task.WhenAny(analysis, Task.Delay(timeout));
            if (completed != analysis)
            {
                // The reference walk may not observe cancellation, but cancel anyway so a regression stops where it can.
                cancellationTokenSource.Cancel();
                Assert.Fail($"Code analysis did not complete within {timeout}.");
            }

            await analysis;

            return (result, logger);
        }
    }
}
