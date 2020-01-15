﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

using FluentAssertions;

using Xunit.Abstractions;
using Xunit;
using System;
using System.IO;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUseVB : SdkTest
    {
        public GivenThatWeWantToUseVB(ITestOutputHelper log) : base(log)
        {
        }

        private enum VBRuntime
        {
            Unknown,
            Default,
            Embedded,
            Referenced
        }

        [Theory]
        [InlineData("net45", true)]
        [InlineData("netstandard2.0", false)]
        [InlineData("netcoreapp2.1", true)]
        [InlineData("netcoreapp3.0", true)]
        [InlineData("netcoreapp3.0", false)]
        public void It_builds_a_simple_vb_project(string targetFramework, bool isExe)
        {
            if (targetFramework == "net45" && !TestProject.ReferenceAssembliesAreInstalled("v4.5"))
            {
                // skip net45 when we do not have .NET Framework 4.5 reference assemblies
                // due to https://github.com/dotnet/core-sdk/issues/3228
                return;
            }

            var (expectedVBRuntime, expectedOutputFiles) = GetExpectedOutputs(targetFramework, isExe);

            var testProject = new TestProject
            {
                Name = "HelloWorld",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                IsExe = isExe,
                SourceFiles =
                {
                    ["Program.vb"] = @"
                        Imports System

                        Module Program
                            #If NETFRAMEWORK Or NETCOREAPP3_0
                                ' https://github.com/dotnet/sdk/issues/2793
                                Private Const TabChar As Char = Chr(9)
                            #End If

                            Function MyComputerName() As String
                                #If NETFRAMEWORK
                                    Return My.Computer.Name
                                #End If

                                #If NETFRAMEWORK Or NETCOREAPP_3_0
                                    ' https://github.com/dotnet/sdk/issues/3379
                                    End
                                #End If
                            End Function

                            Sub Main(args As String())
                                Console.WriteLine(""Hello World from "" & MyComputerName())
                            End Sub
                        End Module
                        ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: targetFramework + isExe, targetExtension: ".vbproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework,
                "VBRuntime")
            {
                DependsOnTargets = "Build"
            };

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var actualVBRuntime = GetVBRuntime(buildCommand.GetValues().FirstOrDefault());
            File.Delete(outputDirectory.File("VBRuntimeValues.txt").FullName);

            outputDirectory.Should().OnlyHaveFiles(expectedOutputFiles);
            actualVBRuntime.Should().Be(expectedVBRuntime);
        }

        private static (VBRuntime, string[]) GetExpectedOutputs(string targetFramework, bool isExe)
        {
            switch ((targetFramework, isExe))
            {
                case ("net45", true):
                    return (VBRuntime.Default, new[]
                    {
                        "HelloWorld.exe",
                        "HelloWorld.exe.config",
                        "HelloWorld.pdb"
                    });

                case ("netcoreapp2.1", true):
                    return (VBRuntime.Embedded, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.runtimeconfig.json",
                        "HelloWorld.runtimeconfig.dev.json",
                        "HelloWorld.deps.json",
                    });

                case ("netcoreapp3.0", true):
                    return (VBRuntime.Referenced, AssertionHelper.AppendApphostOnNonMacOS("HelloWorld", new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.runtimeconfig.json",
                        "HelloWorld.runtimeconfig.dev.json",
                        "HelloWorld.deps.json",
                    }));

                case ("netcoreapp3.0", false):
                   return (VBRuntime.Referenced, new[]
                   {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.deps.json",
                    });

                case ("netstandard2.0", false):
                    return (VBRuntime.Embedded, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.deps.json",
                    });

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static VBRuntime GetVBRuntime(string property)
        {
            switch (property)
            {
                case null:
                    return VBRuntime.Default;

                case "Embed":
                    return VBRuntime.Embedded;

                default:
                    return Path.GetFileName(property) == "Microsoft.VisualBasic.dll"
                        ? VBRuntime.Referenced
                        : VBRuntime.Unknown;
            }
        }

        [WindowsOnlyFact(Skip="https://github.com/dotnet/sdk/issues/3678")]
        public void It_builds_a_vb_wpf_app()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;

            var newCommand = new DotnetCommand(Log, "new", "wpf", "-lang", "vb");
            newCommand.WorkingDirectory = testDirectory;
            newCommand.Execute().Should().Pass();

            var buildCommand = new BuildCommand(Log, testDirectory);
            buildCommand.Execute().Should().Pass();
        }
    }
}
