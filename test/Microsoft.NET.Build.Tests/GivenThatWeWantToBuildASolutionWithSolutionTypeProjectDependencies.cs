// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASolutionWithSolutionTypeProjectDependencies : SdkTest
    {
        public GivenThatWeWantToBuildASolutionWithSolutionTypeProjectDependencies(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_solution_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("SolutionBasedProjectDependencies")
                .WithSource();

            testAsset.Restore(Log, Path.Combine(testAsset.TestRoot, "SolutionWithProjectDependency.sln"));

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, "SolutionWithProjectDependency.sln");
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var netCoreProjectOutput = new DirectoryInfo(Path.Combine(testAsset.Path, "Core", "bin", "Debug", "netcoreapp1.1"));
            var fullFrameworkProjectOutput = new DirectoryInfo(Path.Combine(testAsset.Path, "FF", "bin", "Debug", "net451"));

            netCoreProjectOutput
                .Should()
                .OnlyHaveFiles(new[] {
                    "Core.runtimeconfig.dev.json",
                    "Core.runtimeconfig.json",
                    "Core.deps.json",
                    "Core.dll",
                    "Core.pdb"
                });

            fullFrameworkProjectOutput
                .Should()
                .OnlyHaveFiles(new[] {
                    "FF.exe",
                    "FF.pdb"
                });
        }
    }
}
