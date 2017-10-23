using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Microsoft.Xunit.Performance.Api;
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
        public void BuildNetCore2App()
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

            var perfTest = new PerfTest();
            perfTest.TestName = "Build .NET Core 2 Console App";
            perfTest.ProcessToMeasure = buildCommand.GetProcessStartInfo();
            perfTest.TestFolder = testAsset.TestRoot;

            perfTest.Run();
        }

        [Fact]
        public void BuildNetStandard2App()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var perfTest = new PerfTest();
            perfTest.TestName = "Build .NET Standard 2.0 Library";
            perfTest.ProcessToMeasure = buildCommand.GetProcessStartInfo();
            perfTest.TestFolder = testAsset.TestRoot;

            perfTest.Run();
        }

        [Fact]
        public void BuildMVCApp()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            var newCommand = new DotnetCommand(Log);
            newCommand.WorkingDirectory = testDir.Path;

            newCommand.Execute("new", "mvc").Should().Pass();

            var buildCommand = new BuildCommand(Log, testDir.Path);

            var perfTest = new PerfTest();
            perfTest.TestName = "Build an ASP.NET Core MVC app";
            perfTest.ProcessToMeasure = buildCommand.GetProcessStartInfo();
            perfTest.TestFolder = testDir.Path;

            perfTest.Run();
        }
    }
}
