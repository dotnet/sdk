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
            var solution = workspace.CurrentSolution;
            var projectIds = new List<ProjectId>();
            for (var i = 0; i < 40; i++)
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
                    metadataReferences: [CorlibReference],
                    projectReferences: projectIds.Select(id => new ProjectReference(id))));

                solution = solution.AddDocument(
                    DocumentId.CreateNewId(projectId),
                    $"Class{i}.cs",
                    SourceText.From($"public class Class{i} {{ void M() {{ int unused; }} }}"),
                    filePath: $"/{name}/Class{i}.cs");

                projectIds.Add(projectId);
            }

            var project = solution.GetProject(projectIds[^1]);
            var result = new CodeAnalysisResult(ImmutableHashSet<string>.Empty, ImmutableHashSet<string>.Empty);
            var logger = new TestLogger();

            await new AnalyzerRunner().RunCodeAnalysisAsync(
                result,
                ImmutableArray<DiagnosticAnalyzer>.Empty,
                project,
                ImmutableHashSet.Create(project.Documents.Single().FilePath),
                DiagnosticSeverity.Warning,
                ImmutableHashSet.Create(UnusedVariableDiagnosticId),
                logger,
                CancellationToken.None);

            var diagnostic = Assert.ContainsSingle(Assert.ContainsSingle(result.Diagnostics).Value);
            Assert.AreEqual(UnusedVariableDiagnosticId, diagnostic.Id);
            Assert.IsEmpty(logger.GetLog());
        }
    }
}
