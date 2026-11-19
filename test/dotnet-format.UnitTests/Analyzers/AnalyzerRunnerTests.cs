// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    public class AnalyzerRunnerTests
    {
        private const string UnusedVariableDiagnosticId = "CS0168";

        // AnalyzerRunner treats a project as loaded only if it references mscorlib.dll, so pair the real
        // corlib (needed to compile) with the runtime's mscorlib facade.
        private static readonly ImmutableArray<MetadataReference> RuntimeReferences =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "mscorlib.dll")),
        ];

        [Fact]
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
                    metadataReferences: RuntimeReferences,
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

            var diagnostic = Assert.Single(Assert.Single(result.Diagnostics).Value);
            Assert.Equal(UnusedVariableDiagnosticId, diagnostic.Id);
            Assert.Empty(logger.GetLog());
        }
    }
}
