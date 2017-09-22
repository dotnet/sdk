using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Perf.Tests
{
    public class BuildPerf : SdkTest
    {
        public BuildPerf(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void BuildNetCoreApp()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
