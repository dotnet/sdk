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
    public class GivenThatWeWantToUseFrameworkReferenceIn2x : SdkTest
    {
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

        public GivenThatWeWantToUseFrameworkReferenceIn2x(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        //  TargetFramework, FrameworkReference, RuntimeFrameworkVersion, ExpectedPackageVersion
        [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.App", null, "2.1.1")]
        [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.All", null, "2.1.1")]
        [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.App", "2.1.3", "2.1.3")]

        // TODO enable when 2.2 is released
        // [InlineData("netcoreapp2.2", "Microsoft.AspNetCore.App", null, "2.2.0")]
        // [InlineData("netcoreapp2.2", "Microsoft.AspNetCore.All", null, "2.2.0")]
        public void It_targets_a_known_runtime_framework_name(
            string targetFramework,
            string frameworkReferenceName,
            string runtimeFrameworkVersion,
            string expectedPackageVersion)
        {
            var testProject = new TestProject
            {
                // Keep the test project name short to avoid MAX_PATH issues with MSBuild
                Name = $"FrameworkRef.{targetFramework.Substring(targetFramework.Length - 3)}.{frameworkReferenceName.Substring(frameworkReferenceName.Length - 3)}",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true,
                RuntimeFrameworkVersion = runtimeFrameworkVersion,
                FrameworkReferences =
                {
                    new TestFrameworkReference(frameworkReferenceName)
                }
            };

            testProject.SourceFiles["Program.cs"] = AspNetProgramSource;

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var restoreCommand = testAsset.GetRestoreCommand(Log, testProject.Name);
            restoreCommand.Execute()
                .Should().Pass()
                .And
                .NotHaveStdOutContaining("warning");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            var runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
            var runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            var runtimeConfig = JObject.Parse(runtimeConfigContents);

            var actualRuntimeFrameworkName = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["name"]).Value<string>();
            actualRuntimeFrameworkName.Should().Be(frameworkReferenceName);

            var actualRuntimeFrameworkVersion = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["version"]).Value<string>();
            actualRuntimeFrameworkVersion.Should().Be(expectedPackageVersion);

            var projectAssetsJsonPath = Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json");
            var lockFile = LockFileUtilities.GetLockFile(projectAssetsJsonPath, NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(targetFramework), null);
            var packageLibrary = target.Libraries.Single(l => l.Name == frameworkReferenceName);
            packageLibrary.Version.ToString().Should().Be(expectedPackageVersion);

            target.Libraries.Should().Contain(l => l.Name == "Microsoft.NETCore.App");
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void It_warns_when_explicit_aspnet_package_ref_exists(string packageId)
        {
            var testProject = new TestProject
            {
                Name =  "AspNetCoreWithExplicitRef",
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                PackageReferences =
                {
                    new TestPackageReference(packageId, "2.1.0")
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                testProject.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, testProject.Name);

            restoreCommand.Execute()
                .Should().Pass()
                .And
                .HaveStdOutContaining("warning NETSDK1071:")
                .And
                .HaveStdOutContaining(testProject.Name + ".csproj");

            var lockFile = LockFileUtilities.GetLockFile(
                projectAssetsJsonPath,
                NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(".NETCoreApp,Version=v2.1"), null);
            var metapackageLibrary = target.Libraries.Single(l => l.Name == packageId);
            metapackageLibrary.Version.ToString().Should().Be("2.1.0");
        }

        [Fact]
        public void It_fails_when_unknown_framework_reference_is_used()
        {
            var testProject = new TestProject()
            {
                Name = "UnknownFrameworkReference",
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                FrameworkReferences =
                {
                    new TestFrameworkReference("Banana.App")
                }
            };

            _testAssetsManager
                .CreateTestProject(testProject)
                .GetRestoreCommand(Log, testProject.Name)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("error NETSDK1072:");
        }

        [Fact]
        public void It_generates_deps_file_for_aspnet_app()
        {
            var testProject = new TestProject()
            {
                Name = "AspNetCore21App",
                TargetFrameworks = "netcoreapp2.1",
                IsExe = true,
                IsSdkProject = true,
                FrameworkReferences =
                {
                    new TestFrameworkReference("Microsoft.AspNetCore.App")
                }
            };

            testProject.SourceFiles["Program.cs"] = AspNetProgramSource;

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var projectFolder = Path.Combine(testAsset.Path, testProject.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputFolder = buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName;

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(outputFolder, $"{testProject.Name}.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
                dependencyContext.Should()
                    .OnlyHaveRuntimeAssemblies("", testProject.Name)
                    .And
                    .HaveNoDuplicateRuntimeAssemblies("")
                    .And
                    .HaveNoDuplicateNativeAssets(""); ;
            }
        }
    }
}
