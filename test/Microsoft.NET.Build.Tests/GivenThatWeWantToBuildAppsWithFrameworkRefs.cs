// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAppsWithFrameworkRefs : SdkTest
    {
        public GivenThatWeWantToBuildAppsWithFrameworkRefs(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_projects_successfully()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();

            testAsset.Restore(Log, "EntityFrameworkApp");
            testAsset.Restore(Log, "StopwatchLib");

            VerifyProjectsBuild(testAsset);
        }

        [Fact]
        public void It_builds_with_disable_implicit_frameworkRefs()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();

            testAsset.Restore(Log, "EntityFrameworkApp");
            testAsset.Restore(Log, "StopwatchLib");

            VerifyProjectsBuild(testAsset, "/p:DisableImplicitFrameworkReferences=true");
        }

        void VerifyProjectsBuild(TestAsset testAsset, params string[] buildArgs)
        {
            VerifyBuild(testAsset, "StopwatchLib", "net45", "", buildArgs,
                "StopwatchLib.dll",
                "StopwatchLib.pdb");

            VerifyBuild(testAsset, "EntityFrameworkApp", "net451", "win7-x86", buildArgs,
                "EntityFrameworkApp.exe",
                "EntityFrameworkApp.pdb");

            // Try running EntityFrameworkApp.exe
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "EntityFrameworkApp");
            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory("net451", runtimeIdentifier: "win7-x86");

            Command.Create(Path.Combine(outputDirectory.FullName, "EntityFrameworkApp.exe"), Enumerable.Empty<string>())
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Required Test Provider");
        }

        private void VerifyBuild(TestAsset testAsset, string project, string targetFramework, string runtimeIdentifier,
            string [] buildArgs,
            params string [] expectedFiles)
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, project);

            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);

            buildCommand
                .Execute(buildArgs)
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(expectedFiles);
        }

        [Fact]
        public void The_clean_target_removes_all_files_from_the_output_folder()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences", "CleanTargetRemovesAll")
                .WithSource();

            testAsset.Restore(Log, "EntityFrameworkApp");
            testAsset.Restore(Log, "StopwatchLib");

            VerifyClean(testAsset, "StopwatchLib", "net45", "",
                "StopwatchLib.dll",
                "StopwatchLib.pdb");

            VerifyClean(testAsset, "EntityFrameworkApp", "net451", "win7-x86",
                "EntityFrameworkApp.exe",
                "EntityFrameworkApp.pdb");
        }

        private void VerifyClean(TestAsset testAsset, string project, string targetFramework, string runtimeIdentifier,
            params string[] expectedFiles)
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, project);

            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(expectedFiles);

            var cleanCommand = new MSBuildCommand(Log, "Clean", buildCommand.FullPathProjectFile);

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(Array.Empty<string>());
        }
    }
}
