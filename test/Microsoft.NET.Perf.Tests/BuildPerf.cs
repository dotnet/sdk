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

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: operation.ToString());

            TestProject(testAsset.Path, ".NET Core 2 Console App", operation);
        }

        [Theory]
        [InlineData(ProjectPerfOperation.Build)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData(ProjectPerfOperation.NoOpRestore)]
        public void BuildNetStandard2Library(ProjectPerfOperation operation)
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: operation.ToString());

            TestProject(testAsset.Path, ".NET Standard 2.0 Library", operation);
        }

        [Theory]
        [InlineData(ProjectPerfOperation.Build)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData(ProjectPerfOperation.NoOpRestore)]
        public void BuildMVCApp(ProjectPerfOperation operation)
        {
            var testDir = _testAssetsManager.CreateTestDirectory(identifier: operation.ToString());
            var newCommand = new DotnetCommand(Log);
            newCommand.WorkingDirectory = testDir.Path;

            newCommand.Execute("new", "mvc").Should().Pass();

            TestProject(testDir.Path, "ASP.NET Core MVC app", operation);
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
            var testDir = _testAssetsManager.CreateTestDirectory("Perf_" + name, identifier: operation.ToString());
            FolderSnapshot.MirrorFiles(sourceProject, testDir.Path);

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

            TestProject(testDir.Path, name, operation);
        }

        [Theory]
        [InlineData(ProjectPerfOperation.Build)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildRoslynCompilers(ProjectPerfOperation operation)
        {
            
            string sourceProject = @"C:\git\roslyn";
            var testDir = _testAssetsManager.CreateTestDirectory("Perf_Roslyn", identifier: operation.ToString());
            Console.WriteLine($"Mirroring {sourceProject} to {testDir.Path}...");
            FolderSnapshot.MirrorFiles(sourceProject, testDir.Path);
            Console.WriteLine("Done");
            
            //  Override global.json from repo
            File.Delete(Path.Combine(testDir.Path, "global.json"));

            //  Run Roslyn's restore script
            var restoreCmd = new SdkCommandSpec()
            {
                FileName = Path.Combine(testDir.Path, "Restore.cmd"),
                WorkingDirectory = testDir.Path
            };
            TestContext.Current.AddTestEnvironmentVariables(restoreCmd);
            restoreCmd.ToCommand().Execute().Should().Pass();

            TestProject(Path.Combine(testDir.Path, "Compilers.sln"), "Roslyn", operation);
        }

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

        private void TestProject(string testProject, string testName, ProjectPerfOperation perfOperation)
        {
            string projectFilePath = testProject;

            if (!File.Exists(testProject))
            {
                var slnFiles = Directory.GetFiles(testProject, "*.sln");
                var slnFile = slnFiles.FirstOrDefault();
                if (slnFile != null)
                {
                    projectFilePath = slnFile;
                }
            }

            var restoreCommand = new RestoreCommand(Log, projectFilePath);
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
                commandToTest = new BuildCommand(Log, projectFilePath);
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
            if (File.Exists(testProject))
            {
                perfTest.TestFolder = Path.GetDirectoryName(testProject);
            }
            else
            {
                perfTest.TestFolder = testProject;
            }

            perfTest.Run();
        }
    }
}
