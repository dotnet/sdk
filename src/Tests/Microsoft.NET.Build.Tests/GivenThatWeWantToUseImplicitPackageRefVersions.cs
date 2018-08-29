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
    // Tests for backwards compatibility with ASP.NET Core 2.1 templates
    public class GivenThatWeWantToUseImplicitPackageRefVersions : SdkTest
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

        public GivenThatWeWantToUseImplicitPackageRefVersions(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        //  TargetFramework, FrameworkReference, ExpectedPackageVersion
        [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.App", "2.1.1")]
        [InlineData("netcoreapp2.1", "Microsoft.AspNetCore.All", "2.1.1")]
        public void It_sets_an_implicit_package_ref_version(
            string targetFramework,
            string packageId,
            string expectedPackageVersion)
        {
            var testProject = new TestProject
            {
                Name = $"ImplicitPackageRefVersion.{targetFramework}.{packageId}",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true,
                PackageReferences =
                {
                    new TestPackageReference(packageId, null)
                }
            };

            testProject.SourceFiles["Program.cs"] = AspNetProgramSource;

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var restoreCommand = testAsset.GetRestoreCommand(Log, testProject.Name);
            restoreCommand.Execute()
                .Should().Pass();


            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var projectAssetsJsonPath = Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json");
            var lockFile = LockFileUtilities.GetLockFile(projectAssetsJsonPath, NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(targetFramework), null);
            var packageLibrary = target.Libraries.Single(l => l.Name == packageId);
            packageLibrary.Version.ToString().Should().Be(expectedPackageVersion);
        }
    }
}
