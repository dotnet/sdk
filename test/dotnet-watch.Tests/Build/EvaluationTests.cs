// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class EvaluationTests(ITestOutputHelper output)
    {
        private readonly TestLogger _logger = new TestLogger(output);
        private readonly TestAssetsManager _testAssets = new TestAssetsManager(output);

        private static string MuxerPath
            => TestContext.Current.ToolsetUnderTest.DotNetHostPath;

        private static string InspectPath(string path, string rootDir)
            => path.Substring(rootDir.Length + 1).Replace("\\", "/");

        private static IEnumerable<string> Inspect(string rootDir, IReadOnlyDictionary<string, FileItem> files)
            => files
            .OrderBy(entry => entry.Key)
            .Select(entry => $"{InspectPath(entry.Key, rootDir)}: [{string.Join(", ", entry.Value.ContainingProjectPaths.Select(p => InspectPath(p, rootDir)))}]");

        private static readonly string s_emptyResx = """
            <root>
                <resheader name="resmimetype">
                    <value>text/microsoft-resx</value>
                </resheader>
                <resheader name="version">
                    <value>2.0</value>
                </resheader>
                <resheader name="reader">
                    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                </resheader>
                <resheader name="writer">
                    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                </resheader>
            </root>
            """;

        private static readonly string s_emptyProgram = """
            return 1;
            """;

        [Fact]
        public async Task FindsCustomWatchItems()
        {
            var project = new TestProject("Project1")
            {
                IsExe = true,
                SourceFiles =
                {
                    {"Program.cs", s_emptyProgram},
                    {"app.js", ""},
                    {"gulpfile.js", ""},
                },
                EmbeddedResources =
                {
                    {"Strings.resx", s_emptyResx}
                }
            };

            var testAsset = _testAssets.CreateTestProject(project)
                .WithProjectChanges(d => d.Root!.Add(XElement.Parse("""
                    <ItemGroup>
                      <Watch Include="*.js" Exclude="gulpfile.js" />
                    </ItemGroup>
                    """)));

            await VerifyEvaluation(testAsset,
            [
                new("Project1/Project1.csproj", targetsOnly: true),
                new("Project1/Program.cs"),
                new("Project1/app.js"),
                new("Project1/Strings.resx", targetsOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
            ]);
        }

        [Fact]
        public async Task ExcludesDefaultItemsWithWatchFalseMetadata()
        {
            var project = new TestProject("Project1")
            {
                IsExe = true,
                AdditionalProperties =
                {
                    ["EnableDefaultEmbeddedResourceItems"] = "false",
                    ["EnableDefaultCompileItems"] = "false",
                },
                AdditionalItems =
                {
                    new("EmbeddedResource", new() { { "Include", "*.resx" }, { "Watch", "false" } }),
                    new("Compile", new() { { "Include", "Program.cs" } }),
                    new("Compile", new() { { "Include", "Class*.cs" }, { "Watch", "false" } }),
                },
                SourceFiles =
                {
                    {"Program.cs", s_emptyProgram},
                    {"Class1.cs", ""},
                    {"Class2.cs", ""},
                },
                EmbeddedResources =
                {
                    {"Strings.resx", s_emptyResx },
                }
            };

            var testAsset = _testAssets.CreateTestProject(project);

            await VerifyEvaluation(testAsset,
            [
                new("Project1/Project1.csproj", targetsOnly: true),
                new("Project1/Program.cs"),
                new("Project1/Class1.cs", graphOnly: true),
                new("Project1/Class2.cs", graphOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
            ]);
        }

        [Theory]
        [CombinatorialData]
        public async Task StaticAssets(bool isWeb, [CombinatorialValues(true, false, null)] bool? enableContentFiles)
        {
            var project = new TestProject("Project1")
            {
                ProjectSdk = isWeb ? "Microsoft.NET.Sdk.Web" : "Microsoft.NET.Sdk",
                IsExe = true,
                SourceFiles =
                {
                    {"Program.cs", s_emptyProgram},
                    {"wwwroot/css/app.css", ""},
                    {"wwwroot/js/site.js", ""},
                    {"wwwroot/favicon.ico", ""},
                },
                AdditionalProperties =
                {
                    ["DotNetWatchContentFiles"] = enableContentFiles?.ToString() ?? "",
                },
            };

            var testAsset = _testAssets.CreateTestProject(project, identifier: enableContentFiles.ToString());

            await VerifyEvaluation(testAsset,
                isWeb && enableContentFiles != false ?
                [
                    new("Project1/Project1.csproj", targetsOnly: true),
                    new("Project1/Program.cs"),
                    new("Project1/wwwroot/css/app.css", staticAssetUrl: "wwwroot/css/app.css"),
                    new("Project1/wwwroot/js/site.js", staticAssetUrl: "wwwroot/js/site.js"),
                    new("Project1/wwwroot/favicon.ico", staticAssetUrl: "wwwroot/favicon.ico"),
                    new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                    new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
                ] :
                [
                    new("Project1/Project1.csproj", targetsOnly: true),
                    new("Project1/Program.cs"),
                    new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                    new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
                ]);
        }

        [Fact]
        public async Task RazorClassLibrary()
        {
            var projectRcl = new TestProject("RCL")
            {
                ProjectSdk = "Microsoft.NET.Sdk.Razor",
                PackageReferences =
                {
                    new("Microsoft.AspNetCore.Components.Web", ToolsetInfo.GetPackageVersion("Microsoft.AspNetCore.App.Ref")),
                    new("Microsoft.AspNetCore.Mvc", "2.3.0"),
                },
                SourceFiles =
                {
                    {"Code.cs", ""},
                    {"Page1.razor", ""},
                    {"Page1.razor.css", ""},
                    {"Page2.cshtml", "" },
                    {"Page2.cshtml.css", "" },
                    {"wwwroot/lib.css", ""},
                    {"wwwroot/lib.js", ""},
                }
            };

            var project = new TestProject("Project1")
            {
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                IsExe = true,
                ReferencedProjects = { projectRcl },
                SourceFiles =
                {
                    {"Program.cs", s_emptyProgram},
                    {"wwwroot/css/app.css", ""},
                    {"wwwroot/js/site.js", ""},
                    {"wwwroot/favicon.ico", ""},
                }
            };

            var testAsset = _testAssets.CreateTestProject(project);

            await VerifyEvaluation(testAsset,
            [
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
                new("Project1/Program.cs"),
                new("Project1/Project1.csproj", targetsOnly: true),
                new("Project1/wwwroot/css/app.css", "wwwroot/css/app.css"),
                new("Project1/wwwroot/favicon.ico", "wwwroot/favicon.ico"),
                new("Project1/wwwroot/js/site.js", "wwwroot/js/site.js"),
                new("RCL/Code.cs"),
                new($"RCL/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"RCL/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/EmbeddedAttribute.cs", graphOnly: true),
                new($"RCL/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/RCL.AssemblyInfo.cs", graphOnly: true),
                new($"RCL/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/RCL.GlobalUsings.g.cs", graphOnly: true),
                new($"RCL/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/ValidatableTypeAttribute.cs", graphOnly: true),
                new("RCL/Page1.razor"),
                new("RCL/Page1.razor.css"),
                new("RCL/Page2.cshtml"),
                new("RCL/Page2.cshtml.css"),
                new("RCL/RCL.csproj", targetsOnly: true),
                new("RCL/wwwroot/lib.css", "wwwroot/lib.css"),
                new("RCL/wwwroot/lib.js", "wwwroot/lib.js"),
            ]);
        }

        [Fact]
        public async Task ProjectReferences_OneLevel()
        {
            var project2 = new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.0",
            };

            var project1 = new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                ReferencedProjects = { project2 },
            };

            var testAsset = _testAssets.CreateTestProject(project1);

            await VerifyEvaluation(testAsset, targetFramework: ToolsetInfo.CurrentTargetFramework,
            [
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
                new("Project1/Project1.csproj", targetsOnly: true),
                new("Project1/Project1.cs"),
                new($"Project2/obj/Debug/netstandard2.0/.NETStandard,Version=v2.0.AssemblyAttributes.cs", graphOnly: true),
                new($"Project2/obj/Debug/netstandard2.0/Project2.AssemblyInfo.cs", graphOnly: true),
                new("Project2/Project2.csproj", targetsOnly: true),
                new("Project2/Project2.cs"),
            ]);
        }

        [Fact]
        public async Task TransitiveProjectReferences_TwoLevels()
        {
            var project3 = new TestProject("Project3")
            {
                TargetFrameworks = "netstandard2.0",
            };

            var project2 = new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.0",
                ReferencedProjects = { project3 },
            };

            var project1 = new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                ReferencedProjects = { project2 },
            };

            var testAsset = _testAssets.CreateTestProject(project1);

            await VerifyEvaluation(testAsset, targetFramework: ToolsetInfo.CurrentTargetFramework,
            [
                new($"Project3/obj/Debug/netstandard2.0/.NETStandard,Version=v2.0.AssemblyAttributes.cs", graphOnly: true),
                new($"Project3/obj/Debug/netstandard2.0/Project3.AssemblyInfo.cs", graphOnly: true),
                new("Project3/Project3.csproj", targetsOnly: true),
                new("Project3/Project3.cs"),
                new($"Project2/obj/Debug/netstandard2.0/.NETStandard,Version=v2.0.AssemblyAttributes.cs", graphOnly: true),
                new($"Project2/obj/Debug/netstandard2.0/Project2.AssemblyInfo.cs", graphOnly: true),
                new("Project2/Project2.csproj", targetsOnly: true),
                new("Project2/Project2.cs"),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
                new("Project1/Project1.csproj", targetsOnly: true),
                new("Project1/Project1.cs"),
            ]);
        }

        [Theory]
        [CombinatorialData]
        public async Task SingleTargetRoot_MultiTargetedDependency(bool specifyTargetFramework)
        {
            var project2 = new TestProject("Project2")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
            };

            var project1 = new TestProject("Project1")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                ReferencedProjects = { project2 },
            };

            var testAsset = _testAssets.CreateTestProject(project1, identifier: specifyTargetFramework.ToString());

            await VerifyEvaluation(testAsset, specifyTargetFramework ? ToolsetInfo.CurrentTargetFramework : null,
            [
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"Project1/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project1.AssemblyInfo.cs", graphOnly: true),
                new("Project1/Project1.csproj", targetsOnly: true),
                new("Project1/Project1.cs"),
                new($"Project2/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"Project2/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/Project2.AssemblyInfo.cs", graphOnly: true),
                new($"Project2/obj/Debug/net462/.NETFramework,Version=v4.6.2.AssemblyAttributes.cs", graphOnly: true),
                new($"Project2/obj/Debug/net462/Project2.AssemblyInfo.cs", graphOnly: true),
                new("Project2/Project2.csproj", targetsOnly: true),
                new("Project2/Project2.cs"),
            ]);
        }

        [Fact]
        public async Task FSharpProjectDependency()
        {
            var projectFS = new TestProject("FSProj")
            {
                TargetExtension = ".fsproj",
                AdditionalItems =
                {
                    new("Compile", new() { { "Include", "Lib.fs" } })
                },
                SourceFiles =
                {
                    { "Lib.fs", "module Lib" }
                }
            };

            var projectCS = new TestProject("CSProj")
            {
                ReferencedProjects = { projectFS },
                TargetExtension = ".csproj",
                IsExe = true,
                SourceFiles =
                {
                    { "Program.cs", s_emptyProgram },
                },
            };

            var testAsset = _testAssets.CreateTestProject(projectCS);

            await VerifyEvaluation(testAsset,
            [
                new("CSProj/Program.cs"),
                new($"CSProj/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"CSProj/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/CSProj.AssemblyInfo.cs", graphOnly: true),
                new("CSProj/CSProj.csproj", targetsOnly: true),
                new("FSProj/FSProj.fsproj", targetsOnly: true),
                new("FSProj/Lib.fs"),
                new($"FSProj/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.fs", graphOnly: true),
                new($"FSProj/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/FSProj.AssemblyInfo.fs", graphOnly: true),
            ]);
        }

        [Fact]
        public async Task VBProjectDependency()
        {
            var projectVB = new TestProject("VB")
            {
                TargetExtension = ".vbproj",
                SourceFiles =
                {
                    { "Lib.vb", """
                        Class C
                        End Class
                        """
                    }
                }
            };

            var projectCS = new TestProject("CS")
            {
                ReferencedProjects = { projectVB },
                TargetExtension = ".csproj",
                IsExe = true,
                SourceFiles =
                {
                    { "Program.cs", s_emptyProgram },
                },
            };

            var testAsset = _testAssets.CreateTestProject(projectCS);

            await VerifyEvaluation(testAsset,
            [
                new("CS/Program.cs"),
                new($"CS/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.cs", graphOnly: true),
                new($"CS/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/CS.AssemblyInfo.cs", graphOnly: true),
                new("CS/CS.csproj", targetsOnly: true),
                new("VB/VB.vbproj", targetsOnly: true),
                new("VB/Lib.vb"),
                new($"VB/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/{ToolsetInfo.CurrentTargetFrameworkMoniker}.AssemblyAttributes.vb", graphOnly: true),
                new($"VB/obj/Debug/{ToolsetInfo.CurrentTargetFramework}/VB.AssemblyInfo.vb", graphOnly: true),
            ]);
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

            var options = TestOptions.GetEnvironmentOptions(workingDirectory: testDirectory, muxerPath: MuxerPath);
            var processRunner = new ProcessRunner(processCleanupTimeout: TimeSpan.Zero);
            var buildReporter = new BuildReporter(_logger, new GlobalOptions(), options);

            var filesetFactory = new MSBuildFileSetFactory(projectA, buildArguments: ["/p:_DotNetWatchTraceOutput=true", "/tl:Off"], processRunner, buildReporter);

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
            var prefix = "[Debug]   Collecting watch items from ";
            AssertEx.SequenceEqual(
                [
                    "'A'",
                    "'B'",
                    "'C'",
                    "'D'",
                    "'E'",
                    "'F'",
                    "'G'",
                ],
                _logger.GetAndClearMessages().Where(m => m.Contains(prefix)).Select(m => m.Trim()[prefix.Length..]).Order());
        }

        [Fact]
        public async Task MsbuildOutput()
        {
            var project2 = new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.1",
            };

            var project1 = new TestProject("Project1")
            {
                TargetFrameworks = "net462",
                ReferencedProjects = { project2 },
            };

            var testAsset = _testAssets.CreateTestProject(project1);
            var project1Path = GetTestProjectPath(testAsset);

            var options = TestOptions.GetEnvironmentOptions(workingDirectory: Path.GetDirectoryName(project1Path)!, muxerPath: MuxerPath);
            var processRunner = new ProcessRunner(processCleanupTimeout: TimeSpan.Zero);
            var buildReporter = new BuildReporter(_logger, new GlobalOptions(), options);

            var factory = new MSBuildFileSetFactory(project1Path, buildArguments: ["/tl:Off"], processRunner, buildReporter);
            var result = await factory.TryCreateAsync(requireProjectGraph: null, CancellationToken.None);
            Assert.Null(result);
            // note: msbuild prints errors to stdout, we match the pattern and report as error:
            AssertEx.Equal(
                $"[Error] {project1Path} : error NU1201: Project Project2 is not compatible with net462 (.NETFramework,Version=v4.6.2). Project Project2 supports: netstandard2.1 (.NETStandard,Version=v2.1)",
                _logger.GetAndClearMessages().Single(m => m.Contains("error NU1201")));
        }

        private readonly struct ExpectedFile(string path, string? staticAssetUrl = null, bool targetsOnly = false, bool graphOnly = false)
        {
            public string Path { get; } = path;
            public string? StaticAssetUrl { get; } = staticAssetUrl;
            public bool TargetsOnly { get; } = targetsOnly;
            public bool GraphOnly { get; } = graphOnly;
        }

        private Task VerifyEvaluation(TestAsset testAsset, ExpectedFile[] expectedFiles)
            => VerifyEvaluation(testAsset, targetFramework: null, expectedFiles);

        private async Task VerifyEvaluation(TestAsset testAsset, string? targetFramework, ExpectedFile[] expectedFiles)
        {
            var testDir = testAsset.Path;
            var rootProjectPath = GetTestProjectPath(testAsset);

            output.WriteLine("=== Evaluate using target ===");
            await VerifyTargetsEvaluation();

            output.WriteLine("=== Evaluate using project graph ===");
            await VerifyProjectGraphEvaluation();

            async Task VerifyTargetsEvaluation()
            {
                var options = TestOptions.GetEnvironmentOptions(workingDirectory: testDir, muxerPath: MuxerPath) with { TestOutput = testDir };
                var processRunner = new ProcessRunner(processCleanupTimeout: TimeSpan.Zero);
                var buildArguments = targetFramework != null ? new[] { "/p:TargetFramework=" + targetFramework } : [];
                var buildReporter = new BuildReporter(_logger, new GlobalOptions(), options);
                var factory = new MSBuildFileSetFactory(rootProjectPath, buildArguments, processRunner, buildReporter);
                var targetsResult = await factory.TryCreateAsync(requireProjectGraph: null, CancellationToken.None);
                Assert.NotNull(targetsResult);

                var normalizedActual = Inspect(targetsResult.Files);
                var normalizedExpected = expectedFiles.Where(f => !f.GraphOnly).Select(f => (f.Path, f.StaticAssetUrl)).OrderBy(f => f.Path);
                AssertEx.SequenceEqual(normalizedExpected, normalizedActual);
            }

            async Task VerifyProjectGraphEvaluation()
            {
                // Needs to be executed in dotnet-watch process in order for msbuild to load from the correct location.

                using var watchableApp = new WatchableApp(new DebugTestOutputLogger(output));
                var arguments = targetFramework != null ? new[] { "-f", targetFramework } : [];
                watchableApp.Start(testAsset, arguments, relativeProjectDirectory: testAsset.TestProject!.Name);
                await watchableApp.WaitForOutputLineContaining(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

                var normalizedActual = ParseOutput(watchableApp.Process.Output).OrderBy(f => f.relativePath);
                var normalizedExpected = expectedFiles.Where(f => !f.TargetsOnly).Select(f => (f.Path, f.StaticAssetUrl)).OrderBy(f => f.Path);

                AssertEx.SequenceEqual(normalizedExpected, normalizedActual);
            }

            string GetRelativePath(string fullPath)
                => Path.GetRelativePath(testDir, fullPath).Replace('\\', '/');

            IEnumerable<(string relativePath, string? staticAssetUrl)> Inspect(IReadOnlyDictionary<string, FileItem> files)
                => files.Select(f => (relativePath: GetRelativePath(f.Key), staticAssetUrl: f.Value.StaticWebAssetPath)).OrderBy(f => f.relativePath);

            IEnumerable<(string relativePath, string? staticAssetUrl)> ParseOutput(IEnumerable<string> output)
            {
                foreach (var line in output.SkipWhile(l => !Regex.IsMatch(l, "dotnet watch ⌚ Watching ([0-9]+) file[(]s[)] for changes")).Skip(1))
                {
                    var match = Regex.Match(line, $"> ([^{Path.PathSeparator}]*)({Path.PathSeparator}(.*))?");
                    if (!match.Success)
                    {
                        break;
                    }

                    yield return (GetRelativePath(match.Groups[1].Value), match.Groups[3].Value is { Length: > 0 } value ? value : null);
                }
            }
        }

        private static string GetTestProjectPath(TestAsset projectAsset)
            => Path.Combine(projectAsset.Path, projectAsset.TestProject!.Name!, projectAsset.TestProject!.Name + ".csproj");
    }
}
