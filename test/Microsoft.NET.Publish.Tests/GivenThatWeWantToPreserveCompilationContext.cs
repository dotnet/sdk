// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPreserveCompilationContext
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_publishes_the_project_with_a_refs_folder_and_correct_deps_file()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CompilationContext", "PreserveCompilationContext")
                .WithSource();

            testAsset.Restore("TestApp");
            testAsset.Restore("TestLibrary");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            foreach (var targetFramework in new[] { "net46", "netcoreapp1.0" })
            {
                var publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);

                if (targetFramework == "net46" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    continue;
                }

                publishCommand
                    .Execute($"/p:TargetFramework={targetFramework}")
                    .Should()
                    .Pass();

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework);

                publishDirectory.Should().HaveFiles(new[] {
                    targetFramework == "net46" ? "TestApp.exe" : "TestApp.dll",
                    "TestLibrary.dll",
                    "Newtonsoft.Json.dll"});

                var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
                // Should have compilation time assemblies
                refsDirectory.Should().HaveFile("System.IO.dll");
                // Libraries in which lib==ref should be deduped
                refsDirectory.Should().NotHaveFile("TestLibrary.dll");
                refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");

                using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
                {
                    var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                    dependencyContext.CompilationOptions.Defines.Should().BeEquivalentTo(new[] { "DEBUG", "TRACE" });
                    dependencyContext.CompilationOptions.LanguageVersion.Should().Be("");
                    dependencyContext.CompilationOptions.Platform.Should().Be("AnyCPU");
                    dependencyContext.CompilationOptions.Optimize.Should().Be(false);
                    dependencyContext.CompilationOptions.KeyFile.Should().Be("");
                    dependencyContext.CompilationOptions.EmitEntryPoint.Should().Be(true);
                    dependencyContext.CompilationOptions.DebugType.Should().Be("portable");

                    dependencyContext.CompileLibraries.Count.Should().Be(targetFramework == "net46" ? 51 : 116);
                }
            }
        }
    }
}