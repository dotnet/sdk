// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibraryWithOSMinimumVersion : SdkTest
    {
        public GivenThatWeWantToBuildALibraryWithOSMinimumVersion(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenPropertiesAreNotSetItShouldNotGenerateMinimumOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run");
            runCommand.WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);
            runCommand.Execute()
                .Should()
                .Pass().And.HaveStdOutContaining("NO ATTRIBUTE");
        }

        [Fact]
        public void WhenPropertiesAreSetItCanGenerateMinimumOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject();

            var targetPlatformIdentifier = "iOS";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
            testProject.AdditionalProperties["MinimumOSPlatform"] = "13.2";
            testProject.AdditionalProperties["TargetPlatformVersion"] = "14.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run");
            runCommand.WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);
            runCommand.Execute()
                .Should()
                .Pass().And.HaveStdOutContaining("PlatformName:iOS13.2");
        }

        [Fact]
        public void WhenMinimumOSPlatformISNotSetTargetPlatformVersionIsSetItCanGenerateMinimumOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject();

            var targetPlatformIdentifier = "iOS";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
            testProject.AdditionalProperties["TargetPlatformVersion"] = "13.2";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run");
            runCommand.WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);
            runCommand.Execute()
                .Should()
                .Pass().And.HaveStdOutContaining("PlatformName:iOS13.2");
        }

        [Fact]
        public void WhenMinimumOSPlatformIsHigherThanTargetPlatformVersionItShouldError()
        {
            TestProject testProject = SetUpProject();

            var targetPlatformIdentifier = "iOS";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
            testProject.AdditionalProperties["TargetPlatformVersion"] = "13.2";
            testProject.AdditionalProperties["MinimumOSPlatform"] = "14.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new DotnetBuildCommand(Log, Path.Combine(testAsset.Path, "Project", "Project.csproj"));
            buildCommand.Execute()
                .Should()
                .Fail().And.HaveStdOutContaining("NETSDK1135");
        }

        private static TestProject SetUpProject()
        {
            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = "net5.0",
            };

            testProject.SourceFiles["PrintAttribute.cs"] = _printAttribute;
            return testProject;
        }

        private static readonly string _printAttribute = @"
using System;
using System.Runtime.Versioning;

namespace CustomAttributesTestApp
{
    internal static class CustomAttributesTestApp
    {
        public static void Main()
        {
            var assembly = typeof(CustomAttributesTestApp).Assembly;
            object[] attributes = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.MinimumOSPlatformAttribute), false);
            if (attributes.Length > 0)
            {
                var attribute = attributes[0] as System.Runtime.Versioning.MinimumOSPlatformAttribute;
                Console.WriteLine($""PlatformName:{attribute.PlatformName}"");
            }
            else
            {
                Console.WriteLine(""NO ATTRIBUTE"");
            }
        }
    }
}
";

    }
}
