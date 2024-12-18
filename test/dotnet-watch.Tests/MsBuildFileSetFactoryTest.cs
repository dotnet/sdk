// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;
using Xunit.Sdk;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class MsBuildFileSetFactoryTest(ITestOutputHelper output)
    {
        private readonly TestReporter _reporter = new(output);
        private readonly TestAssetsManager _testAssets = new(output);

        private string MuxerPath
            => TestContext.Current.ToolsetUnderTest.DotNetHostPath;

        private static string InspectPath(string path, string rootDir)
            => path.Substring(rootDir.Length + 1).Replace("\\", "/");

        private static IEnumerable<string> Inspect(string rootDir, IReadOnlyDictionary<string, FileItem> files)
            => files
            .OrderBy(entry => entry.Key)
            .Select(entry => $"{InspectPath(entry.Key, rootDir)}: [{string.Join(", ", entry.Value.ContainingProjectPaths.Select(p => InspectPath(p, rootDir)))}]");

        [Fact]
        public async Task FindsCustomWatchItems()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            });

            project.WithProjectChanges(d => d.Root!.Add(XElement.Parse(
@"<ItemGroup>
    <Watch Include=""*.js"" Exclude=""gulpfile.js"" />
</ItemGroup>")));

            WriteFile(project, "Program.cs");
            WriteFile(project, "app.js");
            WriteFile(project, "gulpfile.js");

            var result = await Evaluate(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Project1.cs",
                    "Program.cs",
                    "app.js"
                },
                result.Files.Keys
            );
        }

        [Fact]
        public async Task ExcludesDefaultItemsWithWatchFalseMetadata()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = "net40",
                AdditionalProperties =
                {
                    ["EnableDefaultEmbeddedResourceItems"] = "false",
                },
            });

            project.WithProjectChanges(d => d.Root!.Add(XElement.Parse(
@"<ItemGroup>
    <EmbeddedResource Include=""*.resx"" Watch=""false"" />
</ItemGroup>")));

            WriteFile(project, "Program.cs");
            WriteFile(project, "Strings.resx");

            var result = await Evaluate(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Project1.cs",
                    "Program.cs",
                },
                result.Files.Keys
            );
        }

        [Fact]
        public async Task SingleTfm()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                AdditionalProperties =
                {
                    ["BaseIntermediateOutputPath"] = "obj",
                },
            });

            WriteFile(project, "Program.cs");
            WriteFile(project, "Class1.cs");
            WriteFile(project, Path.Combine("obj", "Class1.cs"));
            WriteFile(project, Path.Combine("Properties", "Strings.resx"));

            var result = await Evaluate(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Project1.cs",
                    "Program.cs",
                    "Class1.cs",
                    "Properties/Strings.resx",
                },
                result.Files.Keys
            );
        }

        [Fact]
        public async Task MultiTfm()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                AdditionalProperties =
                {
                    ["EnableDefaultCompileItems"] = "false",
                },
            });

            project.WithProjectChanges(d => d.Root!.Add(XElement.Parse(
$@"<ItemGroup>
    <Compile Include=""Class1.netcore.cs"" Condition=""'$(TargetFramework)'=='{ToolsetInfo.CurrentTargetFramework}'"" />
    <Compile Include=""Class1.desktop.cs"" Condition=""'$(TargetFramework)'=='net462'"" />
</ItemGroup>")));

            WriteFile(project, "Class1.netcore.cs");
            WriteFile(project, "Class1.desktop.cs");
            WriteFile(project, "Class1.notincluded.cs");

            var result = await Evaluate(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Class1.netcore.cs",
                    "Class1.desktop.cs",
                },
                 result.Files.Keys
            );
        }

        [Fact]
        public async Task IncludesContentFiles()
        {
            var testDir = _testAssets.CreateTestDirectory();

            var project = WriteFile(testDir, Path.Combine("Project1.csproj"),
@"<Project Sdk=""Microsoft.NET.Sdk.Web"">
    <PropertyGroup>
        <TargetFramework>netstandard2.1</TargetFramework>
    </PropertyGroup>
</Project>");
            WriteFile(testDir, Path.Combine("Program.cs"));

            WriteFile(testDir, Path.Combine("wwwroot", "css", "app.css"));
            WriteFile(testDir, Path.Combine("wwwroot", "js", "site.js"));
            WriteFile(testDir, Path.Combine("wwwroot", "favicon.ico"));

            var result = await Evaluate(project);

            AssertEx.EqualFileList(
                testDir.Path,
                new[]
                {
                    "Project1.csproj",
                    "Program.cs",
                    "wwwroot/css/app.css",
                    "wwwroot/js/site.js",
                    "wwwroot/favicon.ico",
                },
                result.Files.Keys
            );
        }

        [Fact]
        public async Task IncludesContentFilesFromRCL()
        {
            var testDir = _testAssets.CreateTestDirectory();
            WriteFile(
                testDir,
                Path.Combine("RCL1", "RCL1.csproj"),
                $"""
                <Project Sdk="Microsoft.NET.Sdk.Razor">
                    <PropertyGroup>
                        <TargetFramework>netstandard2.1</TargetFramework>
                    </PropertyGroup>
                </Project>
                """);

            WriteFile(testDir, Path.Combine("RCL1", "wwwroot", "css", "app.css"));
            WriteFile(testDir, Path.Combine("RCL1", "wwwroot", "js", "site.js"));
            WriteFile(testDir, Path.Combine("RCL1", "wwwroot", "favicon.ico"));

            var projectPath = WriteFile(
                testDir,
                Path.Combine("Project1", "Project1.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                    <PropertyGroup>
                        <TargetFramework>netstandard2.1</TargetFramework>
                    </PropertyGroup>
                    <ItemGroup>
                        <ProjectReference Include="..\RCL1\RCL1.csproj" />
                    </ItemGroup>
                </Project>
                """);

            WriteFile(testDir, Path.Combine("Project1", "Program.cs"));

            var result = await Evaluate(projectPath);

            AssertEx.EqualFileList(
                testDir.Path,
                new[]
                {
                    "Project1/Project1.csproj",
                    "Project1/Program.cs",
                    "RCL1/RCL1.csproj",
                    "RCL1/wwwroot/css/app.css",
                    "RCL1/wwwroot/js/site.js",
                    "RCL1/wwwroot/favicon.ico",
                },
                result.Files.Keys
            );
        }

        [Fact]
        public async Task ProjectReferences_OneLevel()
        {
            var project2 = _testAssets.CreateTestProject(new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.0",
            });

            var project1 = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                ReferencedProjects = { project2.TestProject, },
            });

            var result = await Evaluate(project1);

            AssertEx.EqualFileList(
                project1.TestRoot,
                new[]
                {
                    "Project2/Project2.csproj",
                    "Project2/Project2.cs",
                    "Project1/Project1.csproj",
                    "Project1/Project1.cs",
                },
                result.Files.Keys
            );
        }

        [Fact]
        public async Task TransitiveProjectReferences_TwoLevels()
        {
            var project3 = _testAssets.CreateTestProject(new TestProject("Project3")
            {
                TargetFrameworks = "netstandard2.0",
            });

            var project2 = _testAssets.CreateTestProject(new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.0",
                ReferencedProjects = { project3.TestProject },
            });

            var project1 = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                ReferencedProjects = { project2.TestProject },
            });

            var result = await Evaluate(project1);

            AssertEx.EqualFileList(
                project1.TestRoot,
                new[]
                {
                    "Project3/Project3.csproj",
                    "Project3/Project3.cs",
                    "Project2/Project2.csproj",
                    "Project2/Project2.cs",
                    "Project1/Project1.csproj",
                    "Project1/Project1.cs",
                },
                result.Files.Keys
            );

            Assert.All(result.Files.Values, f => Assert.False(f.IsStaticFile, $"File {f.FilePath} should not be a static file."));
        }

        [Fact]
        public async Task ProjectReferences_Graph()
        {
            // A->B,F,W(Watch=False)
            // B->C,E
            // C->D
            // D->E
            // F->E,G
            // G->E
            // W->U
            // Y->B,F,Z
            var testDirectory = _testAssets.CopyTestAsset("ProjectReferences_Graph")
                .WithSource()
                .Path;
            var projectA = Path.Combine(testDirectory, "A", "A.csproj");

            var options = new EnvironmentOptions(
                MuxerPath: MuxerPath,
                WorkingDirectory: testDirectory);

            var output = new List<string>();
            _reporter.OnProcessOutput += line => output.Add(line.Content);

            var filesetFactory = new MSBuildFileSetFactory(projectA, targetFramework: null, buildProperties: [("_DotNetWatchTraceOutput", "true")], options, _reporter);

            var result = await filesetFactory.TryCreateAsync(requireProjectGraph: null, CancellationToken.None);
            Assert.NotNull(result);

            AssertEx.SequenceEqual(
            [
                "A/A.cs: [A/A.csproj]",
                "A/A.csproj: [A/A.csproj]",
                "B/B.cs: [B/B.csproj]",
                "B/B.csproj: [B/B.csproj]",
                "C/C.cs: [C/C.csproj]",
                "C/C.csproj: [C/C.csproj]",
                "Common.cs: [A/A.csproj, G/G.csproj]",
                "D/D.cs: [D/D.csproj]",
                "D/D.csproj: [D/D.csproj]",
                "E/E.cs: [E/E.csproj]",
                "E/E.csproj: [E/E.csproj]",
                "F/F.cs: [F/F.csproj]",
                "F/F.csproj: [F/F.csproj]",
                "G/G.cs: [G/G.csproj]",
                "G/G.csproj: [G/G.csproj]",
            ], Inspect(testDirectory, result.Files));

            // ensure each project is only visited once for collecting watch items
            AssertEx.SequenceEqual(
                [
                    "Collecting watch items from 'A'",
                    "Collecting watch items from 'B'",
                    "Collecting watch items from 'C'",
                    "Collecting watch items from 'D'",
                    "Collecting watch items from 'E'",
                    "Collecting watch items from 'F'",
                    "Collecting watch items from 'G'",
                ],
                output.Where(l => l.Contains("Collecting watch items from")).Select(l => l.Trim()).Order());
        }

        [Fact]
        public async Task MsbuildOutput()
        {
            var project2 = _testAssets.CreateTestProject(new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.1",
            });

            var project1 = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = $"net462",
                ReferencedProjects = { project2.TestProject, },
            });

            var project1Path = GetTestProjectPath(project1);

            var options = new EnvironmentOptions(
                MuxerPath: MuxerPath,
                WorkingDirectory: Path.GetDirectoryName(project1Path)!);

            var output = new List<string>();
            _reporter.OnProcessOutput += line => output.Add($"{(line.IsError ? "[stderr]" : "[stdout]")} {line.Content}");

            var factory = new MSBuildFileSetFactory(project1Path, targetFramework: null, buildProperties: [], options, _reporter);
            var result = await factory.TryCreateAsync(requireProjectGraph: null, CancellationToken.None);
            Assert.Null(result);

            // note: msbuild prints errors to stdout:
            AssertEx.Equal(
                $"[stdout] {project1Path} : error NU1201: Project Project2 is not compatible with net462 (.NETFramework,Version=v4.6.2). Project Project2 supports: netstandard2.1 (.NETStandard,Version=v2.1)",
                output.Single(l => l.Contains("error NU1201")));
        }

        private Task<EvaluationResult> Evaluate(TestAsset projectPath)
            => Evaluate(GetTestProjectPath(projectPath));

        private async Task<EvaluationResult> Evaluate(string projectPath)
        {
            var options = new EnvironmentOptions(
                MuxerPath: MuxerPath,
                WorkingDirectory: Path.GetDirectoryName(projectPath)!);

            var factory = new MSBuildFileSetFactory(projectPath, targetFramework: null, buildProperties: [], options, _reporter);
            var result = await factory.TryCreateAsync(requireProjectGraph: null, CancellationToken.None);
            Assert.NotNull(result);
            return result;
        }

        private static string GetTestProjectPath(TestAsset target) => Path.Combine(GetTestProjectDirectory(target), target.TestProject?.Name + ".csproj");

        private static string WriteFile(TestAsset testAsset, string name, string contents = "")
        {
            var path = Path.Combine(GetTestProjectDirectory(testAsset), name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);

            return path;
        }

        private static string WriteFile(TestDirectory testAsset, string name, string contents = "")
        {
            var path = Path.Combine(testAsset.Path, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);

            return path;
        }

        private static string GetTestProjectDirectory(TestAsset testAsset)
            => Path.Combine(testAsset.Path, testAsset.TestProject?.Name ?? string.Empty);
    }
}
