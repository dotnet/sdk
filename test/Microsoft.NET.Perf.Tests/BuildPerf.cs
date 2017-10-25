using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
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


        [Theory]
        [InlineData(ProjectPerfOperation.Build)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData(ProjectPerfOperation.NoOpRestore)]
        public void BuildNetCore2App(ProjectPerfOperation operation)
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            TestProject(testAsset, ".NET Core 2 Console App", operation);
        }

        [Theory]
        [InlineData(ProjectPerfOperation.Build)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData(ProjectPerfOperation.NoOpRestore)]
        public void BuildNetStandard2App(ProjectPerfOperation operation)
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            TestProject(testAsset, ".NET Standard 2.0 Library", operation);
        }

        [Theory]
        [InlineData(ProjectPerfOperation.Build)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData(ProjectPerfOperation.NoOpRestore)]
        public void BuildMVCApp(ProjectPerfOperation operation)
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            var newCommand = new DotnetCommand(Log);
            newCommand.WorkingDirectory = testDir.Path;

            newCommand.Execute("new", "mvc").Should().Pass();

            TestProject(testDir, "ASP.NET Core MVC app", operation);
        }

        [Theory]

        [InlineData("SmallP2POldCsproj", ProjectPerfOperation.Build)]
        [InlineData("SmallP2POldCsproj", ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData("SmallP2POldCsproj", ProjectPerfOperation.NoOpRestore)]
        [InlineData("SmallP2PNewCsproj", ProjectPerfOperation.Build)]
        [InlineData("SmallP2PNewCsproj", ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData("SmallP2PNewCsproj", ProjectPerfOperation.NoOpRestore)]
        [InlineData("LargeP2POldCsproj", ProjectPerfOperation.Build)]
        [InlineData("LargeP2POldCsproj", ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData("LargeP2POldCsproj", ProjectPerfOperation.NoOpRestore)]

        //  This depends on v150 of the VC++, which doesn't exist
        //[InlineData("Generated_100_100_v150")]

        //  Missing dependencies
        //[InlineData("Picasso")]
        public void BuildProjectFromPerfSuite(string name, ProjectPerfOperation operation)
        {
            string sourceProject = Path.Combine(@"C:\MSBPerf\3", name);
            var testDir = _testAssetsManager.CreateTestDirectory("Perf_" + name);
            FolderSnapshot.MirrorFiles(sourceProject, testDir.Path);

            var slnFiles = Directory.GetFiles(testDir.Path, "*.sln");
            var slnFile = slnFiles.First();

            //  The generated projects target .NET Core 2.1, retarget them to .NET Core 2.0
            foreach (var projFile in Directory.GetFiles(testDir.Path, "*.csproj", SearchOption.AllDirectories))
            {
                var project = XDocument.Load(projFile);
                var ns = project.Root.Name.Namespace;

                //  Find both TargetFramework and TargetFrameworks elements
                var targetFrameworkElements = project.Root.Elements(ns + "PropertyGroup").Elements("TargetFramework");
                targetFrameworkElements = targetFrameworkElements.Concat(project.Root.Elements(ns + "PropertyGroup").Elements("TargetFrameworks"));

                foreach (var tfElement in targetFrameworkElements)
                {
                    tfElement.Value = tfElement.Value.Replace("netcoreapp2.1", "netcoreapp2.0");
                }

                project.Save(projFile);
            }

            TestProject(testDir, name, operation);
        }

        //[Fact]
        //public void BuildRoslynCompilers()
        //{
        //    //  Override global.json

        //    //  PerformanceSummary
        //}

        //[Fact]
        //public void RunDotnetTest()
        //{

        //}

        public enum ProjectPerfOperation
        {
            Build,
            BuildWithNoChanges,
            NoOpRestore
        }

        private void TestProject(TestDirectory testDirectory, string testName, ProjectPerfOperation perfOperation)
        {
            var restoreCommand = new RestoreCommand(Log, testDirectory.Path);
            restoreCommand.Execute().Should().Pass();

            TestCommand commandToTest;
            var perfTest = new PerfTest();
            perfTest.ScenarioName = testName;

            if (perfOperation == ProjectPerfOperation.NoOpRestore)
            {
                commandToTest = restoreCommand;
                perfTest.TestName = "Restore (No-op)";
            }
            else
            {
                commandToTest = new BuildCommand(Log, testDirectory.Path);
                if (perfOperation == ProjectPerfOperation.BuildWithNoChanges)
                {
                    commandToTest.Execute().Should().Pass();
                    perfTest.TestName = "Build (no changes)";
                }
                else
                {
                    perfTest.TestName = "Build";
                }
            }

            perfTest.ProcessToMeasure = commandToTest.GetProcessStartInfo();
            perfTest.TestFolder = testDirectory.Path;

            perfTest.Run();
        }
    }
}
