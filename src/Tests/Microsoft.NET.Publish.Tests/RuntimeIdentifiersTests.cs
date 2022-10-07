// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.NET.Publish.Tests
{
    public class RuntimeIdentifiersTests : SdkTest
    {

        TestProject _testProject;

        public RuntimeIdentifiersTests(ITestOutputHelper log) : base(log)
        {
            _testProject = new TestProject("TestProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            _testProject.RecordProperties("RuntimeIdentifier", "SelfContained", "RuntimeIdentifiers");
        }

        //  Run on core MSBuild only as using a local packages folder hits long path issues on full MSBuild
        [CoreMSBuildOnlyFact]
        public void BuildWithRuntimeIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "BuildWithRid",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var compatibleRid = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var runtimeIdentifiers = new[]
            {
                "win-x64",
                "linux-x64",
                compatibleRid
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = string.Join(';', runtimeIdentifiers);

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(testAsset);

            restoreCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var runtimeIdentifier in runtimeIdentifiers)
            {
                var buildCommand = new BuildCommand(testAsset);

                buildCommand
                    .ExecuteWithoutRestore($"/p:RuntimeIdentifier={runtimeIdentifier}")
                    .Should()
                    .Pass();

                if (runtimeIdentifier == compatibleRid)
                {
                    var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: runtimeIdentifier);
                    var selfContainedExecutable = $"{testProject.Name}{Constants.ExeSuffix}";
                    string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

                    new RunExeCommand(Log, selfContainedExecutableFullPath)
                        .Execute()
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOutContaining("Hello World!");
                }
            }
        }

        [Fact]
        public void BuildWithUseCurrentRuntimeIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "BuildWithUseCurrentRuntimeIdentifier",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            var compatibleRid = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "True";

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string targetFrameworkOutputDirectory = Path.Combine(buildCommand.GetNonSDKOutputDirectory().FullName, testProject.TargetFrameworks);
            string outputDirectoryWithRuntimeIdentifier = Directory.EnumerateDirectories(targetFrameworkOutputDirectory, "*", SearchOption.AllDirectories).FirstOrDefault();
            outputDirectoryWithRuntimeIdentifier.Should().NotBeNullOrWhiteSpace();

            var selfContainedExecutable = $"{testProject.Name}{Constants.ExeSuffix}";
            string selfContainedExecutableFullPath = Path.Combine(outputDirectoryWithRuntimeIdentifier, selfContainedExecutable);

            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        //  Run on core MSBuild only as using a local packages folder hits long path issues on full MSBuild
        [CoreMSBuildOnlyTheory]
        [InlineData(false)]
        //  "No build" scenario doesn't currently work: https://github.com/dotnet/sdk/issues/2956
        //[InlineData(true)]
        public void PublishWithRuntimeIdentifier(bool publishNoBuild)
        {
            var testProject = new TestProject()
            {
                Name = "PublishWithRid",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var compatibleRid = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var runtimeIdentifiers = new[]
            {
                "win-x64",
                "linux-x64",
                compatibleRid
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = string.Join(';', runtimeIdentifiers);

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: publishNoBuild ? "nobuild" : string.Empty);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var runtimeIdentifier in runtimeIdentifiers)
            {
                var publishArgs = new List<string>()
                {
                    $"/p:RuntimeIdentifier={runtimeIdentifier}"
                };
                if (publishNoBuild)
                {
                    publishArgs.Add("/p:NoBuild=true");
                }

                var publishCommand = new PublishCommand(testAsset);
                publishCommand.Execute(publishArgs.ToArray())
                    .Should()
                    .Pass();

                if (runtimeIdentifier == compatibleRid)
                {
                    var outputDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: runtimeIdentifier);
                    var selfContainedExecutable = $"{testProject.Name}{Constants.ExeSuffix}";
                    string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

                    new RunExeCommand(Log, selfContainedExecutableFullPath)
                        .Execute()
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOutContaining("Hello World!");

                }
            }
        }

        [Fact]
        public void DuplicateRuntimeIdentifiers()
        {
            var testProject = new TestProject()
            {
                Name = "DuplicateRuntimeIdentifiers",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var compatibleRid = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            testProject.AdditionalProperties["RuntimeIdentifiers"] = compatibleRid + ";" + compatibleRid;
            testProject.RuntimeIdentifier = compatibleRid;

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }

        [Theory]
        [InlineData("net7.0", "true")]
        [InlineData("net7.0", "")]
        [InlineData("net8.0", "")]
        [InlineData("net8.0", "true")]
        [InlineData("net8.0", "false")]
        public void RuntimeSpecificEnablesOrDisablesRuntimeIdentifierByDefaultBasedOnValueAndTargetFramework(string targetFramework, string runtimeSpecific)
        {
            TestAsset testAsset = _testAssetsManager.CreateTestProject(_testProject, identifier: $"{targetFramework}_{runtimeSpecific}")
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeSpecific", runtimeSpecific));
                });

            var expectedRuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            new DotnetBuildCommand(testAsset)
                .Execute($"/p:NETCoreSdkRuntimeIdentifier={expectedRuntimeIdentifier}")
                .Should()
                .Pass();

            if ((runtimeSpecific != "false" && targetFramework == "net8.0") || runtimeSpecific == "true" && targetFramework == "net7.0")
            {
                var properties = _testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework, runtimeIdentifier: expectedRuntimeIdentifier);
                var resolvedRid = properties["RuntimeIdentifier"];
                Assert.True(resolvedRid == expectedRuntimeIdentifier);
            }
            else
            {
                var properties = _testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
                var resolvedRid = properties["RuntimeIdentifier"];
                Assert.True(resolvedRid == "");
            }
        }

        [Theory]
        [InlineData("net7.0")]
        public void RuntimeIdentifiersDisablesRuntimeSpecificFDDBehavior(string targetFramework)
        {
            var expectedRuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            TestAsset testAsset = _testAssetsManager.CreateTestProject(_testProject, identifier: $"RuntimeIdentifierS_{targetFramework}") // the S is intentional to call atention to it
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifiers", $"{expectedRuntimeIdentifier};{expectedRuntimeIdentifier}"));
                });

            new DotnetBuildCommand(testAsset)
                .Execute($"/p:NETCoreSdkRuntimeIdentifier={expectedRuntimeIdentifier}")
                .Should()
                .Pass();

            var properties = _testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework, runtimeIdentifier: expectedRuntimeIdentifier);
            var resolvedRid = properties["RuntimeIdentifier"];
            Assert.True(resolvedRid == "");
        }
    }
}
