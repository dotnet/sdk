// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CodeAnalysis.Tools.Tests.Workspaces
{
    public class BinlogWorkspaceLoaderTests
    {
        [Fact]
        public async Task LoadAsync_WithNonExistentFile_ReturnsNull()
        {
            var logger = NullLogger<Program>.Instance;

            var workspace = await BinlogWorkspaceLoader.LoadAsync(
                "/nonexistent/path/build.binlog",
                logger,
                CancellationToken.None);

            Assert.Null(workspace);
        }

        [Fact]
        public void ParseCommandLine_ExtractsSourceFiles()
        {
            var testDir = Path.Combine(Path.GetTempPath(), $"dotnet-format-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                var file1 = Path.Combine(testDir, "Program.cs");
                var file2 = Path.Combine(testDir, "Helper.cs");
                File.WriteAllText(file1, "class Program { }");
                File.WriteAllText(file2, "class Helper { }");

                var args = new[]
                {
                    "/target:exe",
                    "/langversion:latest",
                    "/nullable:enable",
                    file1,
                    file2
                };

                var parsedArgs = CSharpCommandLineParser.Default.Parse(args, testDir, sdkDirectory: null);

                Assert.Equal(2, parsedArgs.SourceFiles.Length);
                Assert.Contains(parsedArgs.SourceFiles, f => f.Path == file1);
                Assert.Contains(parsedArgs.SourceFiles, f => f.Path == file2);
            }
            finally
            {
                Directory.Delete(testDir, recursive: true);
            }
        }

        [Fact]
        public void ParseCommandLine_ExtractsCompilationOptions()
        {
            var args = new[]
            {
                "/target:library",
                "/langversion:12",
                "/nullable:enable",
                "/define:DEBUG;TRACE",
                "dummy.cs"
            };

            var parsedArgs = CSharpCommandLineParser.Default.Parse(args, "/tmp", sdkDirectory: null);

            Assert.Equal(OutputKind.DynamicallyLinkedLibrary, parsedArgs.CompilationOptions.OutputKind);
            Assert.Equal(NullableContextOptions.Enable, parsedArgs.CompilationOptions.NullableContextOptions);
            Assert.Equal(LanguageVersion.CSharp12, parsedArgs.ParseOptions.LanguageVersion);
        }

        [Fact]
        public async Task CreateWorkspace_WithSourceFiles_AddsDocuments()
        {
            var testDir = Path.Combine(Path.GetTempPath(), $"dotnet-format-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                var file1 = Path.Combine(testDir, "Program.cs");
                var file2 = Path.Combine(testDir, "Helper.cs");
                File.WriteAllText(file1, "class Program { static void Main() { } }");
                File.WriteAllText(file2, "class Helper { }");

                using var workspace = new AdhocWorkspace();
                var projectId = ProjectId.CreateNewId("TestProject");

                var projectInfo = ProjectInfo.Create(
                    projectId,
                    VersionStamp.Default,
                    "TestProject",
                    "TestProject",
                    LanguageNames.CSharp);

                var solution = workspace.CurrentSolution.AddProject(projectInfo);

                foreach (var file in new[] { file1, file2 })
                {
                    var text = await File.ReadAllTextAsync(file);
                    var docId = DocumentId.CreateNewId(projectId, file);
                    solution = solution.AddDocument(docId, Path.GetFileName(file),
                        SourceText.From(text), filePath: file);
                }

                workspace.TryApplyChanges(solution);

                var project = workspace.CurrentSolution.GetProject(projectId)!;
                Assert.Equal(2, project.Documents.Count());
                Assert.Contains(project.Documents, d => d.FilePath == file1);
                Assert.Contains(project.Documents, d => d.FilePath == file2);
            }
            finally
            {
                Directory.Delete(testDir, recursive: true);
            }
        }

        [Fact]
        public async Task CreateWorkspace_CanGetCompilation()
        {
            var testDir = Path.Combine(Path.GetTempPath(), $"dotnet-format-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                var file = Path.Combine(testDir, "Test.cs");
                File.WriteAllText(file, "class Test { public int Value { get; set; } }");

                using var workspace = new AdhocWorkspace();
                var projectId = ProjectId.CreateNewId("TestProject");

                var projectInfo = ProjectInfo.Create(
                    projectId,
                    VersionStamp.Default,
                    "TestProject",
                    "TestProject",
                    LanguageNames.CSharp,
                    compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                var solution = workspace.CurrentSolution.AddProject(projectInfo);

                var text = await File.ReadAllTextAsync(file);
                var docId = DocumentId.CreateNewId(projectId, file);
                solution = solution.AddDocument(docId, "Test.cs", SourceText.From(text), filePath: file);

                workspace.TryApplyChanges(solution);

                var project = workspace.CurrentSolution.GetProject(projectId)!;
                var compilation = await project.GetCompilationAsync();

                Assert.NotNull(compilation);
                Assert.Contains(compilation!.SyntaxTrees, t => t.FilePath == file);
            }
            finally
            {
                Directory.Delete(testDir, recursive: true);
            }
        }
    }
}
