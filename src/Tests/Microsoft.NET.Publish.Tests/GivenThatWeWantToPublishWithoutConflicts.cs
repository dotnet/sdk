// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishWithoutConflicts : SdkTest
    {
        public GivenThatWeWantToPublishWithoutConflicts(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_solves_conflicts_between_package_and_implicit_references()
        {
            // Test case from https://github.com/dotnet/sdk/issues/3904.
            // This dll is included in both the explicit package reference and Microsoft.NET.Build.Extensions. We prevent a double write in 
            // _ComputeResolvedCopyLocalPublishAssets by removing dlls duplicated between package references and implicitly expanded .NET references.
            var reference = "System.Runtime.InteropServices.RuntimeInformation";
            var targetFramework = "net461";
            var testProject = new TestProject()
            {
                Name = "ConflictingFilePublish",
                IsSdkProject = true,
                TargetFrameworks = targetFramework
            };
            testProject.PackageReferences.Add(new TestPackageReference(reference, "4.3.0"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore", "2.1.4"));
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), targetFramework, "ResolvedFileToPublish", GetValuesCommand.ValueType.Item)
           {
                DependsOnTargets = "Publish"
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var files = getValuesCommand.GetValues()
                .Where(file => file.Contains(reference));
            files.Count().Should().Be(1);
            // We should choose the file from system.runtime.interopservices.runtimeinformation package version
            files.FirstOrDefault().Contains(@"system.runtime.interopservices.runtimeinformation\4.3.0").Should().BeTrue();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_has_consistent_behavior_when_publishing_single_file(bool shouldPublishSingleFile)
        {
            var targetFramework = "netcoreapp3.1";
            var testProject = new TestProject()
            {
                Name = "DuplicateFiles",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true,
                RuntimeIdentifier = "win-x64"
            };
            // The Microsoft.TestPlatform.CLI package contains System.Runtime.CompilerServices.Unsafe.dll as content, which could cause a double write with the same dll originating from the 
            // runtime package. Without _HandleFileConflictsForPublish this would be caught when by the bundler when publishing single file, but a normal publish would succeed with double writes.
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.TestPlatform.CLI", "16.5.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), targetFramework, "ResolvedFileToPublish", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Publish"
            };

            if (shouldPublishSingleFile) {
                getValuesCommand.Execute("/p:PublishSingleFile=true")
                    .Should()
                    .Pass();
            }
            else
            {
                getValuesCommand.Execute()
                    .Should()
                    .Pass();

                var duplicatedDll = "System.Runtime.CompilerServices.Unsafe.dll";
                var files = getValuesCommand.GetValues()
                    .Where(file => file.Contains(duplicatedDll));
                files.Count().Should().Be(1);
                // We should choose the file from microsoft.netcore.app.runtime package over the Microsoft.TestPlatform.CLI package version
                files.FirstOrDefault().Contains("microsoft.netcore.app.runtime").Should().BeTrue();
            }
        }
    }
}
