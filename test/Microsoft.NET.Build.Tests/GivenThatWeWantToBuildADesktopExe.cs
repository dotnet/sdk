﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using FluentAssertions;
using Xunit;

using Xunit.Abstractions;
using System.Text.RegularExpressions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExe : SdkTest
    {
        public GivenThatWeWantToBuildADesktopExe(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_a_simple_desktop_app()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var targetFramework = "net45";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Element(ns + "TargetFramework").SetValue(targetFramework);
                })
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.exe",
                "HelloWorld.pdb",
            });
        }

        [Theory]

        // If we don't set platformTarget and don't use native dependency, we get working AnyCPU app.
        [InlineData("defaults", null, false, "Native code was not used (MSIL)")]

        // If we don't set platformTarget and do use native dependency, we get working x86 app.
        [InlineData("defaultsNative", null, true, "Native code was used (X86)")]

        // If we set x86 and don't use native dependency, we get working x86 app.
        [InlineData("x86", "x86", false, "Native code was not used (X86)")]

        // If we set x86 and do use native dependency, we get working x86 app.
        [InlineData("x86Native", "x86", true, "Native code was used (X86)")]

        // If we set x64 and don't use native dependency, we get working x64 app.
        [InlineData("x64", "x64", false, "Native code was not used (Amd64)")]

        // If we set x64 and do use native dependency, we get working x64 app.
        [InlineData("x64Native", "x64", true, "Native code was used (Amd64)")]

        // If we set AnyCPU and don't use native dependency, we get working  AnyCPU app.
        [InlineData("AnyCPU", "AnyCPU", false, "Native code was not used (MSIL)")]

        // If we set AnyCPU and do use native dependency, we get any CPU app that can't find its native dependency.
        // Tests current behavior, but ideally we'd also raise a build diagnostic in this case: https://github.com/dotnet/sdk/issues/843
        [InlineData("AnyCPUNative", "AnyCPU", true, "Native code failed (MSIL)")]
        public void It_handles_native_depdencies_and_platform_target(
             string identifier,
             string platformTarget,
             bool useNativeCode,
             string expectedProgramOutput)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            foreach (bool multiTarget in new[] { false, true })
            {
                var testAsset = _testAssetsManager
                   .CopyTestAsset("DesktopMinusRid", identifier: Path.DirectorySeparatorChar + identifier + (multiTarget ? "Multi" : ""))
                   .WithSource()
                   .WithProjectChanges(project =>
                   {
                       var ns = project.Root.Name.Namespace;
                       var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                       propertyGroup.Add(new XElement(ns + "UseNativeCode", useNativeCode));

                       if (platformTarget != null)
                       {
                           propertyGroup.Add(new XElement(ns + "PlatformTarget", platformTarget));
                       }

                       if (multiTarget)
                       {
                           propertyGroup.Element(ns + "TargetFramework").Remove();
                           propertyGroup.Add(new XElement(ns + "TargetFrameworks", "net46;netcoreapp1.1"));
                       }
                   })
                  .Restore(Log);

                var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
                buildCommand
                    .Execute()
                    .Should()
                    .Pass();

                var exe = Path.Combine(buildCommand.GetOutputDirectory("net46").FullName, "DesktopMinusRid.exe");
                var runCommand = Command.Create(exe, Array.Empty<string>());
                runCommand
                    .CaptureStdOut()
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining(expectedProgramOutput);
            }
        }

        [Theory]

        // implict rid with option to append rid to output path off -> do not append
        [InlineData("implicitOff", "", false, false)]

        // implicit rid with option to append rid to output path on -> do not append (never append implicit rid irrespective of option)
        [InlineData("implicitOn", "", true, false)]

        // explicit  rid with option to append rid to output path off -> do not append
        [InlineData("explicitOff", "win7-x86", false, false)]
        
        // explicit rid with option to append rid to output path on -> append
        [InlineData("explicitOn", "win7-x64", true, true)]
        public void It_appends_rid_to_outdir_correctly(string identifier, string rid, bool useAppendOption, bool shouldAppend)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            foreach (bool multiTarget in new[] { false, true })
            {
                var testAsset = _testAssetsManager
                    .CopyTestAsset("DesktopMinusRid", identifier: Path.DirectorySeparatorChar + identifier + (multiTarget ? "Multi" : ""))
                    .WithSource()
                    .WithProjectChanges(project =>
                    {
                        var ns = project.Root.Name.Namespace;
                        var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", rid));
                        propertyGroup.Add(new XElement(ns + "AppendRuntimeIdentifierToOutputPath", useAppendOption.ToString()));

                        if (multiTarget)
                        {
                            propertyGroup.Element(ns + "RuntimeIdentifier").Add(new XAttribute("Condition", "'$(TargetFramework)' == 'net46'"));
                            propertyGroup.Element(ns + "TargetFramework").Remove();
                            propertyGroup.Add(new XElement(ns + "TargetFrameworks", "net46;netcoreapp1.1"));
                        }
                    })
                    .Restore(Log);

                var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
                buildCommand
                    .Execute()
                    .Should()
                    .Pass();

                var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
                publishCommand
                    .Execute(multiTarget ? new[] { "/p:TargetFramework=net46" } : Array.Empty<string>())
                    .Should()
                    .Pass();

                string expectedOutput;
                switch (rid)
                {
                    case "":
                        expectedOutput = "Native code was not used (MSIL)";
                        break;

                    case "win7-x86":
                        expectedOutput = "Native code was not used (X86)";
                        break;

                    case "win7-x64":
                        expectedOutput = "Native code was not used (Amd64)";
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(rid));
                }

                var outputDirectory = buildCommand.GetOutputDirectory("net46", runtimeIdentifier: shouldAppend ? rid : "");
                var publishDirectory = publishCommand.GetOutputDirectory("net46", runtimeIdentifier: rid);

                foreach (var directory in new[] { outputDirectory, publishDirectory })
                {
                    var exe = Path.Combine(directory.FullName, "DesktopMinusRid.exe");

                    var runCommand = Command.Create(exe, Array.Empty<string>());
                    runCommand
                        .CaptureStdOut()
                        .Execute()
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOutContaining(expectedOutput);
                }
            }
        }


        [Theory]
        [InlineData("win7-x86", "x86")]
        [InlineData("win8-x86-aot", "x86")]
        [InlineData("win7-x64", "x64")]
        [InlineData("win8-x64-aot", "x64")]
        [InlineData("win10-arm", "arm")]
        [InlineData("win10-arm-aot", "arm")]
        //PlatformTarget=arm64 is not supported and never inferred
        [InlineData("win10-arm64", "AnyCPU")]
        [InlineData("win10-arm64-aot", "AnyCPU")]
        // cpu architecture is never expected at the front
        [InlineData("x86-something", "AnyCPU")]
        [InlineData("x64-something", "AnyCPU")]
        [InlineData("arm-something", "AnyCPU")]
        public void It_builds_with_inferred_platform_target(string runtimeIdentifier, string expectedPlatformTarget)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopMinusRid", identifier: Path.DirectorySeparatorChar + runtimeIdentifier)
                .WithSource()
                .Restore(Log, "", $"/p:RuntimeIdentifier={runtimeIdentifier}");

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot,
                "net46", "PlatformTarget", GetValuesCommand.ValueType.Property);

            getValuesCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo(expectedPlatformTarget);
        }

        [Fact]
        public void It_respects_explicit_platform_target()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopMinusRid")
                .WithSource()
                .Restore(Log, "", $"/p:RuntimeIdentifier=win7-x86");

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot,
                "net46", "PlatformTarget", GetValuesCommand.ValueType.Property);

            getValuesCommand
                .Execute($"/p:RuntimeIdentifier=win7-x86", "/p:PlatformTarget=x64")
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo("x64");
        }

        [Fact]
        public void It_includes_default_framework_references()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "DefaultReferences",
                //  TODO: Add net35 to the TargetFrameworks list once https://github.com/Microsoft/msbuild/issues/1333 is fixed
                TargetFrameworks = "net40;net45;net461",
                IsSdkProject = true,
                IsExe = true
            };

            string sourceFile =
@"using System;

namespace DefaultReferences
{
    public class TestClass
    {
        public static void Main(string [] args)
        {
            var uri = new System.Uri(""http://github.com/dotnet/corefx"");
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        }
    }
}";
            testProject.SourceFiles.Add("TestClass.cs", sourceFile);

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, "DefaultReferences");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, "DefaultReferences"));

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Could not resolve this reference", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        }

        [Fact]
        public void It_reports_a_single_failure_if_reference_assemblies_are_not_found()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "MissingReferenceAssemblies",
                //  A version of .NET we don't expect to exist
                TargetFrameworks = "net469",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            //  Pass "/clp:summary" so that we can check output for string "1 Error(s)"
            var result = buildCommand.Execute("/clp:summary");

            result.Should().Fail();

            //  Error code for reference assemblies not found
            result.StdOut.Should().Contain("MSB3644");

            //  Error code for exception generated from task
            result.StdOut.Should().NotContain("MSB4018");

            //  Ensure no other errors are generated
            result.StdOut.Should().Contain("1 Error(s)");
        }

        [Fact]
        public void It_does_not_report_conflicts_if_the_same_framework_assembly_is_referenced_multiple_times()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "DuplicateFrameworkReferences",
                TargetFrameworks = "net461",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "Reference", new XAttribute("Include", "System")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [Fact]
        public void It_does_not_report_conflicts_when_referencing_a_nuget_package()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "DesktopConflictsNuGet",
                TargetFrameworks = "net461",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference",
                                    new XAttribute("Include", "NewtonSoft.Json"),
                                    new XAttribute("Version", "9.0.1")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [Fact]
        public void It_does_not_report_conflicts_when_with_http_4_1_package()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "DesktopConflictsHttp4_1",
                TargetFrameworks = "net461",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Net.Http", "4.1.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            //  Verify that ResolveAssemblyReference doesn't generate any conflicts
            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("MSB3243", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [WindowsOnlyFact]
        public void It_does_not_report_conflicts_with_runtime_specific_items()
        {
            var testProject = new TestProject()
            {
                Name = "DesktopConflictsRuntimeTargets",
                TargetFrameworks = "net461",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties["PlatformTarget"] = "AnyCPU";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference",
                                    new XAttribute("Include", "System.Security.Cryptography.Algorithms"),
                                    new XAttribute("Version", "4.3.0")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var buildResult = buildCommand
                .Execute("/v:normal");

            buildResult.Should().Pass();

            //  Correct asset should be copied to output folder. Before fixing https://github.com/dotnet/sdk/issues/1510,
            //  the runtimeTargets items would win conflict resolution, and then would not be copied to the output folder,
            //  so there'd be no copy of the DLL in the output folder.
            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().HaveFile("System.Security.Cryptography.Algorithms.dll");

            //  There should be no conflicts
            buildResult.Should().NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [Fact]
        public void It_generates_binding_redirects_if_needed()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource()
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("net452", runtimeIdentifier: "win7-x86");

            outputDirectory.Should().HaveFiles(new[] {
                "DesktopNeedsBindingRedirects.exe",
                "DesktopNeedsBindingRedirects.exe.config"
            });
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_places_package_satellites_correctly(bool crossTarget)
        {
            var testProject = new TestProject()
            {
                Name = "DesktopUsingPackageWithSatellites",
                TargetFrameworks = "net46",
                IsSdkProject = true,
                IsExe = true
            };

            if (crossTarget)
            {
                testProject.Name += "_cross";
            }

            testProject.PackageReferences.Add(new TestPackageReference("FluentValidation", "5.5.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    if (crossTarget)
                    {
                        var ns = project.Root.Name.Namespace;
                        var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        propertyGroup.Element(ns + "TargetFramework").Name += "s";
                    }
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().NotHaveFile("FluentValidation.resources.dll");
            outputDirectory.Should().HaveFile(@"fr\FluentValidation.resources.dll");
        }
    }
}
