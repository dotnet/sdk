// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToRunFromMSBuildTarget : SdkTest
    {
        public GivenThatWeWantToRunFromMSBuildTarget(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_runs_successfully()
        {
            TestProject testProject = new TestProject()
            {
                Name = "TestRunTargetProject",
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var runTargetCommand = new MSBuildCommand(Log, "run", Path.Combine(testAsset.TestRoot, testProject.Name));
            runTargetCommand
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Hello World!");
        }
    }
}
