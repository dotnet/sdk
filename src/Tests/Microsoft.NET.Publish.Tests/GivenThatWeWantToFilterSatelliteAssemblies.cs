// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using System.Xml.Linq;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToFilterSatelliteAssemblies : SdkTest
    {
        public GivenThatWeWantToFilterSatelliteAssemblies(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netcoreapp3.0")]

        public void It_only_publishes_selected_ResourceLanguages(string tfm)
        {
            var testProject = new TestProject()
            {
                Name = "PublishFilteredSatelliteAssemblies",
                TargetFrameworks = tfm,
                IsExe = true,
                IsSdkProject = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Spatial", "5.8.3"));
            testProject.AdditionalProperties.Add("SatelliteResourceLanguages", "en-US;it;fr");

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.TestRoot, testProject.Name));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks);

            var files = new List<string>() {
                "it/System.Spatial.resources.dll",
                "fr/System.Spatial.resources.dll",
                "System.Spatial.dll",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json"
            };

            if (tfm == "netcoreapp3.0" && RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin)
            {
                files.Add($"{testProject.Name}{Constants.ExeSuffix}");
            }

            publishDirectory.Should().OnlyHaveFiles(files);
        }
        [Fact]
        public void It_publishes_all_satellites_when_not_filtered()
        {
            var testProject = new TestProject()
            {
                Name = "PublishSatelliteAssemblies",
                TargetFrameworks = "netcoreapp2.0",
                IsExe = true,
                IsSdkProject = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Spatial", "5.8.3"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.TestRoot, testProject.Name));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks);

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "de/System.Spatial.resources.dll",
                "es/System.Spatial.resources.dll",
                "fr/System.Spatial.resources.dll",
                "it/System.Spatial.resources.dll",
                "ja/System.Spatial.resources.dll",
                "ko/System.Spatial.resources.dll",
                "ru/System.Spatial.resources.dll",
                "zh-Hans/System.Spatial.resources.dll",
                "zh-Hant/System.Spatial.resources.dll",
                "System.Spatial.dll",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json"
            });
        }
    }
}
