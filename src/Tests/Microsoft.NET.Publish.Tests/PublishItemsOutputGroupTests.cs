﻿using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishItemsOutputGroupTests : SdkTest
    {
        public PublishItemsOutputGroupTests(ITestOutputHelper log) : base(log)
        {
        }

        private readonly static List<string> FrameworkAssemblies = new List<string>()
        {
            "api-ms-win-core-console-l1-1-0.dll",
            "System.Runtime.dll",
            "WindowsBase.dll",
        };

        [Fact]
        public void GroupPopulatedWithRid()
        {
            var testProject = this.SetupProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, testAsset.Path, testProject.Name);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, testAsset.Path, testProject.Name);
            buildCommand
                .Execute("/p:RuntimeIdentifier=win-x86;DesignTimeBuild=true", "/t:PublishItemsOutputGroup")
                .Should()
                .Pass();

            var testOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput"));
            Log.WriteLine("Contents of PublishItemsOutputGroup dumped to '{0}'.", testOutputDir.FullName);

            // Check for the existence of a few specific files that should be in the directory where the 
            // contents of PublishItemsOutputGroup were dumped to make sure it's getting populated.
            testOutputDir.Should().HaveFile($"{testProject.Name}.exe");
            testOutputDir.Should().HaveFile($"{testProject.Name}.deps.json");
            testOutputDir.Should().HaveFiles(FrameworkAssemblies);

            var testKeyOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput_Key"));
            Log.WriteLine("PublishItemsOutputGroup key items dumped to '{0}'.", testKeyOutputDir.FullName);

            // Verify the only key item is the exe
            testKeyOutputDir.Should().OnlyHaveFiles(new List<string>() { $"{testProject.Name}.exe" });
        }

        [Fact]
        public void GroupNotPopulatedWithoutRid()
        {
            var testProject = this.SetupProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, testAsset.Path, testProject.Name);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, testAsset.Path, testProject.Name);
            buildCommand
                .Execute("/p:DesignTimeBuild=true", "/t:PublishItemsOutputGroup")
                .Should()
                .Pass();

            var testOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput"));
            Log.WriteLine("Contents of PublishItemsOutputGroup dumped to '{0}'.", testOutputDir.FullName);

            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin)
            {
                testOutputDir.Should().HaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }

            testOutputDir.Should().HaveFile($"{testProject.Name}.deps.json");

            // Since no RID was specified the output group should not contain framework assemblies
            testOutputDir.Should().NotHaveFiles(FrameworkAssemblies);

            var testKeyOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput_Key"));
            Log.WriteLine("PublishItemsOutputGroup key items dumped to '{0}'.", testKeyOutputDir.FullName);

            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin)
            {
                // Verify the only key item is the exe
                testKeyOutputDir.Should()
                    .OnlyHaveFiles(new List<string>() {$"{testProject.Name}{Constants.ExeSuffix}"});
            }
        }

        [CoreMSBuildAndWindowsOnlyFact]
        public void GroupPopulatedCorrectlyWithSingleFile()
        {
            var testProject = this.SetupProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, testAsset.Path, testProject.Name);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, testAsset.Path, testProject.Name);
            buildCommand
                .Execute("/p:RuntimeIdentifier=win-x86;DesignTimeBuild=true;PublishSingleFile=true", "/t:PublishItemsOutputGroup")
                .Should()
                .Pass();

            var testOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput"));
            Log.WriteLine("Contents of PublishItemsOutputGroup dumped to '{0}'.", testOutputDir.FullName);

            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin)
            {
                testOutputDir.Should().HaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }

            // In the single file case there shouldn't be a deps.json file 
            testOutputDir.Should().NotHaveFile($"{testProject.Name}.deps.json");

            // The framework assemblies should also get bundled with the main exe
            testOutputDir.Should().NotHaveFiles(FrameworkAssemblies);

            var testKeyOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput_Key"));
            Log.WriteLine("PublishItemsOutputGroup key items dumped to '{0}'.", testKeyOutputDir.FullName);

            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin)
            {
                // Verify the only key item is the exe
                testKeyOutputDir.Should()
                    .OnlyHaveFiles(new List<string>() { $"{testProject.Name}{Constants.ExeSuffix}" });
            }
        }

        private TestProject SetupProject()
        {
            var testProject = new TestProject()
            {
                Name = "TestPublishOutputGroup",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = "win-x86";

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            // Add a target that will dump the contents of the PublishItemsOutputGroup to
            // a test directory after building.
            testProject.CopyFilesTargets.Add(new CopyFilesTarget(
                "CopyPublishItemsOutputGroup",
                "PublishItemsOutputGroup",
                "@(PublishItemsOutputGroupOutputs)",
                null,
                "$(MSBuildProjectDirectory)\\TestOutput"));

            // Add another target that will dump the members of PublishItemsOutputGroup that
            // have property IsKeyOutput set to true to a different test directory.
            testProject.CopyFilesTargets.Add(new CopyFilesTarget(
                "CopyPublishKeyItemsOutputGroup",
                "PublishItemsOutputGroup",
                "@(PublishItemsOutputGroupOutputs)",
                @"'%(PublishItemsOutputGroupOutputs.IsKeyOutput)' == 'True'",
                "$(MSBuildProjectDirectory)\\TestOutput_Key"));

            return testProject;
        }
    }
}
