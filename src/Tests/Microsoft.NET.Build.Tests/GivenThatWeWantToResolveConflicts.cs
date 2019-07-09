﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToResolveConflicts : SdkTest
    {
        public GivenThatWeWantToResolveConflicts(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netstandard2.0")]
        public void The_same_references_are_used_with_or_without_DisableDefaultPackageConflictOverrides(string targetFramework)
        {
            var defaultProject = new TestProject()
            {
                Name = "DefaultProject",
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };
            AddConflictReferences(defaultProject);
            GetReferences(
                defaultProject,
                expectConflicts: false,
                references: out List<string> defaultReferences,
                referenceCopyLocalPaths: out List<string> defaultReferenceCopyLocalPaths);

            var disableProject = new TestProject()
            {
                Name = "DisableProject",
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };
            disableProject.AdditionalProperties.Add("DisableDefaultPackageConflictOverrides", "true");
            AddConflictReferences(disableProject);
            GetReferences(
                disableProject,
                expectConflicts: true,
                references: out List<string> disableReferences,
                referenceCopyLocalPaths: out List<string> disableReferenceCopyLocalPaths);

            Assert.Equal(defaultReferences, disableReferences);
            Assert.Equal(defaultReferenceCopyLocalPaths, disableReferenceCopyLocalPaths);
        }

        private void AddConflictReferences(TestProject testProject)
        {
            foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
            {
                testProject.PackageReferences.Add(new TestPackageReference(dependency.Item1, dependency.Item2));
            }
        }

        private void GetReferences(TestProject testProject, bool expectConflicts, out List<string> references, out List<string> referenceCopyLocalPaths)
        {
            string targetFramework = testProject.TargetFrameworks;
            TestAsset tempTestAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            string projectFolder = Path.Combine(tempTestAsset.TestRoot, testProject.Name);

            var getReferenceCommand = new GetValuesCommand(
                Log,
                projectFolder,
                targetFramework,
                "Reference",
                GetValuesCommand.ValueType.Item);
            getReferenceCommand.DependsOnTargets = "Build";
            var result = getReferenceCommand.Execute("/v:detailed").Should().Pass();
            if (expectConflicts)
            {
                result.And.HaveStdOutMatching("Encountered conflict", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }
            else
            {
                result.And.NotHaveStdOutMatching("Encountered conflict", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }

            references = getReferenceCommand.GetValues();

            var getReferenceCopyLocalPathsCommand = new GetValuesCommand(
                Log,
                projectFolder,
                targetFramework,
                "ReferenceCopyLocalPaths",
                GetValuesCommand.ValueType.Item);
            getReferenceCopyLocalPathsCommand.DependsOnTargets = "Build";
            getReferenceCopyLocalPathsCommand.Execute().Should().Pass();

            referenceCopyLocalPaths = getReferenceCopyLocalPathsCommand.GetValues();
        }

        [Fact]
        public void CompileConflictsAreNotRemovedFromRuntimeDepsAssets()
        {
            TestProject testProject = new TestProject()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true,
                //  In deps file, assets are under the ".NETStandard,Version=v2.0/" target (ie with empty RID) for some reason
                RuntimeIdentifier = string.Empty
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Mvc.Razor", "2.0.1"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            string projectFolder = Path.Combine(testAsset.Path, testProject.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier).FullName;

            string depsJsonPath = Path.Combine(outputFolder, $"{testProject.Name}.deps.json");

            var assets = DepsFileSkipTests.GetDepsJsonAssets(depsJsonPath, testProject, "runtime")
                .Select(DepsFileSkipTests.GetDepsJsonFilename)
                .ToList();

            assets.Should().Contain("System.ValueTuple.dll");

        }

        [Fact]
        public void AProjectCanReferenceADllInAPackageDirectly()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ReferencePackageDllDirectly",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.VisualStudio.Composition", "15.8.112"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = p.Root.Element(ns + "ItemGroup");
                    itemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", @"$(NuGetPackageRoot)/microsoft.visualstudio.composition/15.8.112/lib/net45/Microsoft.VisualStudio.Composition.dll"),
                        new XAttribute("Private", "true")));
                })
                .Restore(Log, testProject.Name);

            string projectFolder = Path.Combine(testAsset.Path, testProject.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void DuplicateFrameworkAssembly()
        {
            TestProject testProject = new TestProject()
            {
                Name = "DuplicateFrameworkAssembly",
                TargetFrameworks = "net472",
                IsSdkProject = true,
                IsExe = true
            };
            testProject.References.Add("System.Runtime");
            testProject.References.Add("System.Runtime");
            testProject.PackageReferences.Add(new TestPackageReference("System.Runtime", "4.3.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            string projectFolder = Path.Combine(testAsset.Path, testProject.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void FilesFromAspNetCoreSharedFrameworkAreNotIncluded()
        {
            var testProject = new TestProject()
            {
                Name = "AspNetCoreProject",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Extensions.DependencyInjection.Abstractions", "2.2.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                    new XAttribute("Include", "Microsoft.AspNetCore.App")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, testProject.Name);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().NotHaveFile("Microsoft.Extensions.DependencyInjection.Abstractions.dll");
        }
    }
}
