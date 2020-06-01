// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToSpecifyOSInTargetFramework : SdkTest
    {
        public GivenThatWeWantToSpecifyOSInTargetFramework(ITestOutputHelper log) : base(log)
        {}

        [Theory]
        [InlineData("windows")]
        [InlineData("macos")]
        [InlineData("ios")]
        [InlineData("android")]
        public void It_passes_on_supported_os(string targetPlatform)
        {
            TestProject testProject = new TestProject()
            {
                Name = "SupportedOS",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatform;

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var build = new BuildCommand(Log, Path.Combine(testAsset.Path, testProject.Name));
            build.Execute() 
                .Should()
                .Pass();
        }

        [Fact]
        public void It_fails_on_unsupported_os()
        {
            TestProject testProject = new TestProject()
            {
                Name = "UnsupportedOS",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "customos";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var build = new BuildCommand(Log, Path.Combine(testAsset.Path, testProject.Name));
            build.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1132");
        }
    }
}
