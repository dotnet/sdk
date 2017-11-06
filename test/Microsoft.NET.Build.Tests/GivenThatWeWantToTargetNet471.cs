// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
#pragma warning disable xUnit1004 // Test methods should not be skipped

    public class GivenThatWeWantToTargetNet471 : SdkTest
    {
        public GivenThatWeWantToTargetNet471(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_a_net471_app()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "Net471App",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
            });
        }

        [Fact]
        public void It_builds_a_net471_app_referencing_netstandard20()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_NetStandard20",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var netStandardProject = new TestProject()
            {
                Name="NetStandard20_Library",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_ns20")
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{netStandardProject.Name}.dll",
                $"{netStandardProject.Name}.pdb",

                "System.Net.Http.dll",
                "System.IO.Compression.dll",
            });
        }

        [Fact]
        public void It_does_not_include_facades_from_nuget_packages()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "Net471_NuGetFacades",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("NETStandard.Library", "1.6.1"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",

                "System.Net.Http.dll",
                "System.IO.Compression.dll",

                //  This is an implementation dependency of the System.Net.Http package, which won't get conflict resolved out
                "System.Diagnostics.DiagnosticSource.dll",
            });
        }

        [Theory]
        [InlineData("netstandard1.4")]
        [InlineData("netstandard2.0")]
        public void It_uses_updated_httpClient_and_compression(string netstandardVersion)
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "Net471_HttpClient",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.References.Add("System.Net.Http");
            testProject.References.Add("System.IO.Compression");

            var referencedProject = new TestProject()
            {
                Name = "NetStandardProject",
                TargetFrameworks = netstandardVersion,
                IsSdkProject = true,
                IsExe = false
            };

            testProject.ReferencedProjects.Add(referencedProject);

            testProject.SourceFiles["Program.cs"] = @"
using System;
using System.Net.Http;
using System.Net.Http.Headers;

public static class Program
{
    public static void Main()
    {
        HttpClient httpClient = Class1.GetHttpClient();

        //  AuthenticationHeaderValue is IClonable in .NET Framework and in version 4.2.0.0 of the contract
        //  (which is the version from ImplicitlyExpandNETStandardFacades), but not in prior versions of the
        //  contract, which would come from the package closure of 1.x versions of NETStandard.Library
        ICloneable cloneable = new AuthenticationHeaderValue(""scheme"");
    }
}";

            referencedProject.SourceFiles["Class1.cs"] = @"
using System.Net.Http;

public class Class1
{
    public static HttpClient GetHttpClient()
    {
        return new HttpClient();
    }
}
";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, netstandardVersion)
               .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().HaveFiles(new[]
            {
                "System.Net.Http.dll",
                "System.IO.Compression.dll"
            });

            var httpAssemblyName = AssemblyName.GetAssemblyName(Path.Combine(outputDirectory.FullName, "System.Net.Http.dll"));
            httpAssemblyName.Version.Should().Be(new Version(4, 2, 0, 0));

            var compressionAssemblyName = AssemblyName.GetAssemblyName(Path.Combine(outputDirectory.FullName, "System.IO.Compression.dll"));
            httpAssemblyName.Version.Should().Be(new Version(4, 2, 0, 0));
        }

        [Fact]
        public void It_does_not_include_httpClient_and_compression_if_netstandard_isnt_referenced()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "Net471_No_NetStandard",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.References.Add("System.Net.Http");
            testProject.References.Add("System.IO.Compression");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
               .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().NotHaveFiles(new[]
            {
                "System.Net.Http.dll",
                "System.IO.Compression.dll"
            });
        }

        static bool Net471ReferenceAssembliesAreInstalled()
        {
            var net461referenceAssemblies = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version461);
            if (net461referenceAssemblies == null)
            {
                //  4.6.1 reference assemblies not found, assume that 4.7.1 isn't available either
                return false;
            }
            var net471referenceAssemblies = Path.Combine(new DirectoryInfo(net461referenceAssemblies).Parent.FullName, "v4.7.1");
            return Directory.Exists(net471referenceAssemblies);
        }

    }
}
