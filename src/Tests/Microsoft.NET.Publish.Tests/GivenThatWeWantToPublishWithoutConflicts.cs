// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
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

        [Fact]
        public void It_publishes_with_package_reference()
        {
            var reference = "System.Runtime.InteropServices.RuntimeInformation";
            var targetFramework = "net461";
            var testProject = new TestProject()
            {
                Name = "ConflitingFilePublish",
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
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_publishes_single_file(bool shouldPublishSingleFile)
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
            }
        }
    }
}
