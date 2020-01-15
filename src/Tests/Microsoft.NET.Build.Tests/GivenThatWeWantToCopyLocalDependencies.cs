// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using FluentAssertions;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToCopyLocalDependencies : SdkTest
    {
        public GivenThatWeWantToCopyLocalDependencies(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_copies_local_package_dependencies_on_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var expectedFiles = new []
            {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.dev.json",
                $"{ProjectName}.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            };

            outputDirectory.Should().OnlyHaveFiles(AssertionHelper.AppendApphostOnNonMacOS(ProjectName, expectedFiles));
        }

        [Fact]
        public void It_does_not_copy_local_package_dependencies_when_requested_not_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "false";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute().Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(AssertionHelper.AppendApphostOnNonMacOS(ProjectName, new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.dev.json",
                $"{ProjectName}.runtimeconfig.json",
            }));
        }

        //  Core MSBuild only because CI machines don't have updated VS (with support for RuntimeIdentifierGraphPath)
        [CoreMSBuildOnlyFact]
        public void It_copies_local_specific_runtime_package_dependencies_on_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            var rid = EnvironmentInfo.GetCompatibleRid("netcoreapp3.0");

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true
            };

            testProject.AdditionalProperties.Add("RuntimeIdentifier", rid);
            testProject.AdditionalProperties.Add("SelfContained", "false");
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks, runtimeIdentifier: rid);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.dev.json",
                $"{ProjectName}.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                // NOTE: this may break in the future when the SDK supports platforms that sqlite does not
                $"{FileConstants.DynamicLibPrefix}sqlite3{FileConstants.DynamicLibSuffix}"
            });
        }

        [Fact]
        public void It_does_not_copy_local_package_dependencies_for_lib_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = false
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
            });
        }

        [Fact]
        public void It_copies_local_package_dependencies_for_lib_projects_when_requested_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = false
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            });
        }

        [Fact]
        public void It_does_not_copy_local_package_dependencies_for_netstandard_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netstandard2.0"
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb"
            });
        }

        [Fact]
        public void It_copies_local_package_dependencies_for_netstandard_projects_when_requested_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netstandard2.0"
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.AdditionalProperties["CopyLocalRuntimeTargetAssets"] = "true";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            });
        }

        [Fact]
        public void It_does_not_copy_local_runtime_dependencies_for_netframework_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "net46"
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
            });
        }

        //  Core MSBuild only because CI machines don't have updated VS (with support for RuntimeIdentifierGraphPath)
        [CoreMSBuildOnlyFact]
        public void It_copies_local_all_assets_on_self_contained_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            var rid = EnvironmentInfo.GetCompatibleRid("netcoreapp3.0");

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true
            };

            testProject.AdditionalProperties.Add("RuntimeIdentifier", rid);
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", "11.0.2"));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

             var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, ProjectName);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks, runtimeIdentifier: rid);

            outputDirectory.Should().HaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.dev.json",
                $"{ProjectName}.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                // NOTE: this may break in the future when the SDK supports platforms that sqlite does not
                $"{FileConstants.DynamicLibPrefix}sqlite3{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}clrjit{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                $"mscorlib.dll",
                // This is not an exhaustive list as there are many files in self-contained builds
            });

            outputDirectory.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });
        }
    }
}
