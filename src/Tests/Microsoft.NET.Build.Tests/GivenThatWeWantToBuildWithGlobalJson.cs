// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithGlobalJson : SdkTest
    {
        public GivenThatWeWantToBuildWithGlobalJson(ITestOutputHelper log) : base(log)
        {}

        [FullMSBuildOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_fails_build_on_failed_sdk_resolution(bool runningInVS)
        {
            TestProject testProject = new TestProject()
            {
                Name = "FailedResolution",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var globalJsonPath = Path.Combine(testAsset.Path, testProject.Name, "global.json");
            File.WriteAllText(globalJsonPath, @"{
""sdk"": {
""version"": ""9.9.999""
}
}");

            var resolveSdkCommand = new ResolveSdkCommand(testAsset);
            var result = resolveSdkCommand.Execute($"/p:BuildingInsideVisualStudio={runningInVS}")
                .Should()
                .Fail();
            var warningString = "warning : Unable to locate the .NET SDK as specified by global.json, please check that the specified version is installed.";
            var errorString = "Unable to locate the .NET SDK. Check that it is installed and that the version specified in global.json (if any) matches the installed version.";
            if (runningInVS)
            {
                result.And
                    .HaveStdOutContaining(warningString)
                    .And
                    .NotHaveStdOutContaining(errorString);
            }
            else
            {
                result.And
                    .HaveStdOutContaining(errorString)
                    .And
                    .NotHaveStdOutContaining(warningString);
            }
        }
    }
}
