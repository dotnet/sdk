// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithoutTransitiveProjectRefs : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithoutTransitiveProjectRefs(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_builds_the_project_successfully_when_RAR_finds_all_references()
        {
            BuildAppWithTransitiveDependenciesAndTransitiveCompileReference(new []{"/p:DisableTransitiveProjectReferences=true"});
        }
        
        [WindowsOnlyFact]
        public void It_builds_the_project_successfully_with_static_graph_and_isolation()
        {
            BuildAppWithTransitiveDependenciesAndTransitiveCompileReference(new []{"/graph", "/isolate"});
        }
        
        [WindowsOnlyFact]
        public void It_cleans_the_project_successfully_with_static_graph_and_isolation()
        {
            var (testAsset, outputDirectories) = BuildAppWithTransitiveDependenciesAndTransitiveCompileReference(new []{"/graph", "/isolate"});

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

        private (TestAsset TestAsset, IReadOnlyDictionary<string, DirectoryInfo> OutputDirectories) BuildAppWithTransitiveDependenciesAndTransitiveCompileReference(string[] msbuildArguments)
        {
            // NOTE the project dependencies: 1->2->4->5; 1->3->4->5 

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveDependenciesAndTransitiveCompileReference")
                .WithSource();

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
                                "5.pdb",
                            }));

                DotnetCommand runCommand = new DotnetCommand(
                    Log,
                    "run",
                    $"--framework",
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

        [Fact]
        public void It_builds_the_project_successfully_when_RAR_does_not_find_all_references()
        {
            // NOTE the project dependencies: 1->2->3

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveDependenciesButNoTransitiveCompileReference")
                .WithSource();

            testAsset.Restore(Log, "1");

            var (buildResult, outputDirectories) = Build(testAsset, new []{"netcoreapp2.1"}, new []{"/p:DisableTransitiveProjectReferences=true"});

            buildResult.Should().Pass();

            outputDirectories.Should().ContainSingle().Which.Key.Should().Be("netcoreapp2.1");

            var outputDirectory = outputDirectories.First().Value;

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "1.dll",
                "1.pdb",
                "1.deps.json",
                "1.runtimeconfig.json",
                "1.runtimeconfig.dev.json",
                "2.dll",
                "2.pdb",
            });

            new DotnetCommand(Log, Path.Combine(outputDirectory.FullName, "1.dll"))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World from 1");
        }

        private (CommandResult BuildResult, IReadOnlyDictionary<string, DirectoryInfo> OutputDirectories) Build(
            TestAsset testAsset,
            IEnumerable<string> targetFrameworks,
            string[] msbuildArguments
            )
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "1");

            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            var buildResult = buildCommand.Execute(msbuildArguments);

            var outputDirectories = targetFrameworks.ToImmutableDictionary(tf => tf, tf => buildCommand.GetOutputDirectory(tf));

            return (buildResult, outputDirectories);
        }
    }
}
