// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.Build.Tasks;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToSetRuntimeFrameworkName : SdkTest
    {
        private const string AspNetTestPackageVersion = "2.1.3-feature-metapackage-chain-30910";
        private const string ConsoleProgramSource = @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Hello World!"");
    }
}
";
        private const string AspNetProgramSource = @"
using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

public class Program
{
    public static void Main(string[] args)
    {
        WebHost.CreateDefaultBuilder(args).Build().Run();
    }
}
";

        public GivenThatWeWantToSetRuntimeFrameworkName(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        //  TargetFramework, RuntimeFrameworkName, RuntimeFrameworkVersion, ExpectedPackageVersion
        [InlineData("netcoreapp2.1", "Microsoft.NETCore.App", null, "2.1.0")]
        // TODO uncomment once 2.1.3 ships
        // [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.App", null, "2.1.3")]
        // [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.All", null, "2.1.3")]
        [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.App", AspNetTestPackageVersion, AspNetTestPackageVersion)]
        [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.All", AspNetTestPackageVersion, AspNetTestPackageVersion)]
        public void It_targets_a_known_runtime_framework_name(
            string targetFramework,
            string runtimeFrameworkName,
            string runtimeFrameworkVersion,
            string expectedPackageVersion)
        {
            string testIdentifier = "SharedRuntimeTargeting_" + string.Join("_", targetFramework, runtimeFrameworkName, runtimeFrameworkVersion ?? "null");

            var testProject = new TestProject
            {
                Name = "FrameworkTargetTest",
                TargetFrameworks = targetFramework,
                RuntimeFrameworkVersion = runtimeFrameworkVersion,
                IsSdkProject = true,
                IsExe = true,
                RuntimeFrameworkName = runtimeFrameworkName,
            };

            testProject.SourceFiles["Program.cs"] = runtimeFrameworkName.Contains("AspNetCore")
                ? AspNetProgramSource
                : ConsoleProgramSource;

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, testIdentifier)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);

            string actualRuntimeFrameworkName = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["name"]).Value<string>();
            actualRuntimeFrameworkName.Should().Be(runtimeFrameworkName);

            string actualRuntimeFrameworkVersion = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["version"]).Value<string>();
            actualRuntimeFrameworkVersion.Should().Be(expectedPackageVersion);

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(targetFramework), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == runtimeFrameworkName);
            netCoreAppLibrary.Version.ToString().Should().Be(expectedPackageVersion);
        }


        [Fact]
        public void It_fails_when_unknown_runtimeframework_name_is_used()
        {
            TestProject project = new TestProject()
            {
                Name = "UnknownFrameworkName",
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                RuntimeFrameworkName = "Banana.App",
            };

            var testAsset = _testAssetsManager.CreateTestProject(project);
            var restoreCommand = testAsset.GetRestoreCommand(Log, project.Name);

            restoreCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("error NETSDK1070:");
        }

        [Fact]
        public void It_generates_deps_file_for_aspnet_app()
        {
            TestProject project = new TestProject()
            {
                Name = "AspNetCore21App",
                TargetFrameworks = "netcoreapp2.1",
                IsExe = true,
                IsSdkProject = true,
                RuntimeFrameworkName = "Microsoft.AspNetCore.App",
                RuntimeFrameworkVersion = AspNetTestPackageVersion,
            };

            project.SourceFiles["Program.cs"] = AspNetProgramSource;

            var testAsset = _testAssetsManager.CreateTestProject(project)
                .Restore(Log, project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(project.TargetFrameworks).FullName;

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(outputFolder, $"{project.Name}.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
                dependencyContext.Should()
                    .OnlyHaveRuntimeAssemblies("", project.Name)
                    .And
                    .HaveNoDuplicateRuntimeAssemblies("")
                    .And
                    .HaveNoDuplicateNativeAssets(""); ;
            }
        }
    }
}
