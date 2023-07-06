﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    public class GivenThatWeWantToBuildAnAppWithLibrariesAndRid : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithLibrariesAndRid(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_a_RID_specific_runnable_output()
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            string[] msbuildArgs = new[]
            {
                $"/p:RuntimeIdentifier={runtimeIdentifier}",
                $"/p:TestRuntimeIdentifier={runtimeIdentifier}"
            };

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid")
                .WithSource();

            var restoreCommand = new RestoreCommand(testAsset, "App");
            restoreCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(testAsset, "App");

            buildCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"App{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void It_builds_a_framework_dependent_RID_specific_runnable_output()
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryAndRid", "BuildFrameworkDependentRIDSpecific")
                .WithSource();

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                //  .NET Core 2.1.0 packages don't support latest versions of OS X, so roll forward to the
                //  latest patch which does
                testAsset = testAsset.WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("TargetLatestRuntimePatch", true));
                });
            }

            var restoreCommand = new RestoreCommand(testAsset, "App");
            restoreCommand
                .Execute($"/p:TestRuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(testAsset, "App");

            buildCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}", $"/p:TestRuntimeIdentifier={runtimeIdentifier}", "/p:SelfContained=false")
                .Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: runtimeIdentifier);

            outputDirectory.Should().NotHaveSubDirectories();

            string[] expectedFiles = new[] {
                $"App{Constants.ExeSuffix}",
                "App.dll",
                "App.pdb",
                "App.deps.json",
                "App.runtimeconfig.json",
                "LibraryWithoutRid.dll",
                "LibraryWithoutRid.pdb",
                "LibraryWithRid.dll",
                "LibraryWithRid.pdb",
                "LibraryWithRids.dll",
                "LibraryWithRids.pdb",
                $"{FileConstants.DynamicLibPrefix}sqlite3{FileConstants.DynamicLibSuffix}"
            };

            outputDirectory.Should().OnlyHaveFiles(expectedFiles.Where(x => !String.IsNullOrEmpty(x)).ToList() );

            new DotnetCommand(Log, Path.Combine(outputDirectory.FullName, "App.dll"))
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining($"3.13.0 '{runtimeIdentifier}' 3.13.0 '{runtimeIdentifier}' Hello World");
        }
    }
}
