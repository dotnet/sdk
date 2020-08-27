// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithStaticGraph : SdkTest
    {
        public GivenThatWeWantToBuildWithStaticGraph(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_project_successfully_with_static_graph()
        {
            Build(new []{"/graph"});
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/13081")]
        public void It_builds_the_project_successfully_with_static_graph_and_isolation()
        {
            Build(new []{"/graph", "/isolate"});
        }

        [Fact]
        public void It_cleans_the_project_successfully_with_static_graph()
        {
            var (testAsset, outputDirectories) = Build(new []{"/graph", "/isolate"});

            var cleanCommand = new DotnetCommand(
                Log,
                "msbuild",
                Path.Combine(testAsset.TestRoot, "1", "1.csproj"),
                "/t:clean",
                "/graph",
                "/isolate");

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var outputDirectory in outputDirectories)
            {
                outputDirectory.Value.GetFileSystemInfos()
                    .Should()
                    .BeEmpty();
            }
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/13081")]
        public void It_cleans_the_project_successfully_with_static_graph_and_isolation()
        {
            var (testAsset, outputDirectories) = Build(new []{"/graph", "/isolate"});

            var cleanCommand = new DotnetCommand(
                Log,
                "msbuild",
                Path.Combine(testAsset.TestRoot, "1", "1.csproj"),
                "/t:clean",
                "/graph",
                "/isolate");

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var outputDirectory in outputDirectories)
            {
                outputDirectory.Value.GetFileSystemInfos()
                    .Should()
                    .BeEmpty();
            }
        }

        private (TestAsset TestAsset, IReadOnlyDictionary<string, DirectoryInfo> OutputDirectories)
            Build(string[] msbuildArguments, [CallerMemberName] string callingMethod = "")
        {
            var testAsset = _testAssetsManager.CreateTestProject(DiamondShapeGraphWithRuntimeDependencies(), callingMethod);

            testAsset.Restore(Log, "1");

            string[] targetFrameworks = {"netcoreapp2.1", "net472"};

            var (buildResult, outputDirectories) = Build(testAsset, targetFrameworks, msbuildArguments);

            buildResult.Should()
                .Pass();

            var coreExeFiles = new[]
            {
                "1.dll",
                "1.pdb",
                "1.deps.json",
                "1.runtimeconfig.json",
                "1.runtimeconfig.dev.json"
            };

            var netFrameworkExeFiles = new[]
            {
                "1.exe",
                "1.pdb",
                "1.exe.config",
                "System.Diagnostics.DiagnosticSource.dll"
            };

            foreach (var targetFramework in targetFrameworks)
            {
                var runtimeFiles = targetFramework.StartsWith("netcoreapp")
                    ? coreExeFiles
                    : netFrameworkExeFiles;

                outputDirectories[targetFramework].Should()
                    .OnlyHaveFiles(
                        runtimeFiles.Concat(
                            new[]
                            {
                                "2.dll",
                                "2.pdb",
                                "3.dll",
                                "3.pdb",
                                "4.dll",
                                "4.pdb",
                                "5.dll",
                                "5.pdb"
                            }));

                if (targetFramework.StartsWith("net4") && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // only windows can build full framework tfms
                    break;
                }

                DotnetCommand runCommand = new DotnetCommand(
                    Log,
                    "run",
                    "--framework",
                    targetFramework,
                    "--project",
                    Path.Combine(testAsset.TestRoot, "1", "1.csproj"));

                runCommand
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("Hello World from 1")
                    .And
                    .HaveStdOutContaining("Hello World from 2")
                    .And
                    .HaveStdOutContaining("Hello World from 4")
                    .And
                    .HaveStdOutContaining("Hello World from 5")
                    .And
                    .HaveStdOutContaining("Hello World from 3")
                    .And
                    .HaveStdOutContaining("Hello World from 4")
                    .And
                    .HaveStdOutContaining("Hello World from 5");
            }

            return (testAsset, outputDirectories);
        }

        private (CommandResult BuildResult, IReadOnlyDictionary<string, DirectoryInfo> OutputDirectories) Build(
            TestAsset testAsset,
            IEnumerable<string> targetFrameworks,
            string[] msbuildArguments
            )
        {
            var buildCommand = new BuildCommand(testAsset, "1");
            var buildResult = buildCommand.ExecuteWithoutRestore(msbuildArguments);

            var outputDirectories = targetFrameworks.ToImmutableDictionary(tf => tf, tf => buildCommand.GetOutputDirectory(tf));

            return (buildResult, outputDirectories);
        }

        private const string SourceFile = @"
using System;

namespace _{0}
{{
    public class Class1
    {{
        static void Main(string[] args)
        {{
            Message();
        }}

        public static void Message()
        {{
            Console.WriteLine(""Hello World from {0}"");
            {1}
        }}
    }}
}}
";

        private TestProject DiamondShapeGraphWithRuntimeDependencies()
        {
            var project5 = new TestProject
            {
                Name = "5",
                IsSdkProject = true,
                TargetFrameworks = "netstandard1.3",
                SourceFiles =
                {
                    ["Program.cs"] = string.Format(SourceFile, "5", string.Empty)
                }
            };
            
            var project4 = new TestProject
            {
                Name = "4",
                IsSdkProject = true,
                TargetFrameworks = "netstandard1.3;netstandard1.6;net461",
                ReferencedProjects = { project5 },
                SourceFiles =
                {
                    ["Program.cs"] = string.Format(SourceFile, "4", "_5.Class1.Message();")
                }
            };
            
            var project3 = new TestProject
            {
                Name = "3",
                IsSdkProject = true,
                TargetFrameworks = "netstandard2.0;net462",
                ReferencedProjects = { project4 },
                SourceFiles =
                {
                    ["Program.cs"] = string.Format(SourceFile, "3", "_4.Class1.Message();")
                }
            };
            
            var project2 = new TestProject
            {
                Name = "2",
                IsSdkProject = true,
                TargetFrameworks = "netstandard1.5",
                ReferencedProjects = { project4 },
                SourceFiles =
                {
                    ["Program.cs"] = string.Format(SourceFile, "2", "_4.Class1.Message();")
                }
            };
            
            var project1 = new TestProject
            {
                Name = "1",
                IsExe = true,
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.1;net472",
                ReferencedProjects = { project2, project3 },
                SourceFiles =
                {
                    ["Program.cs"] = string.Format(SourceFile, "1", " _2.Class1.Message(); _3.Class1.Message();")
                }
            };

            return project1;
        }
    }
}
