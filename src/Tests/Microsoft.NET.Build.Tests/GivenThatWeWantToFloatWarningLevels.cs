// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToFloatWarningLevels : SdkTest
    {
        private const string targetFrameworkNet6 = "net6.0";
        private const string targetFrameworkNetFramework472 = "net472";

        public GivenThatWeWantToFloatWarningLevels(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(targetFrameworkNet6, "6")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkVersion)]
        [InlineData(targetFrameworkNetFramework472, "4")]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_defaults_WarningLevel_To_The_Current_TFM_When_Net(string tfm, string warningLevel)
        {
            int parsedWarningLevel = (int)double.Parse(warningLevel);
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = tfm,
                IsExe = true,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                }
                            }
                        }
                    ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "warningLevelConsoleApp" + tfm, targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                tfm, "WarningLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            var computedWarningLevel = buildCommand.GetValues()[0];
            buildResult.StdErr.Should().Be(string.Empty);
            computedWarningLevel.Should().Be(parsedWarningLevel.ToString());
        }

        [InlineData(1, "1")]
        [InlineData(null, ToolsetInfo.CurrentTargetFrameworkVersion)]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_always_accepts_user_defined_WarningLevel(int? warningLevel, string expectedWarningLevel)
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                }
                            }
                        }
                    ",
                }
            };
            testProject.AdditionalProperties.Add("WarningLevel", warningLevel?.ToString());
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "customWarningLevelConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                ToolsetInfo.CurrentTargetFramework, "WarningLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            var computedWarningLevel = buildCommand.GetValues()[0];
            buildResult.StdErr.Should().Be(string.Empty);
            computedWarningLevel.Should().Be(((int)float.Parse(expectedWarningLevel)).ToString());
        }

        [InlineData(targetFrameworkNet6, "6.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkVersion)]
        [InlineData(targetFrameworkNetFramework472, null)]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_defaults_AnalysisLevel_To_The_Current_TFM_When_NotLatestTFM(string tfm, string analysisLevel)
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = tfm,
                IsExe = true,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                }
                            }
                        }
                    ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelConsoleApp" + tfm, targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                tfm, "EffectiveAnalysisLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();

            buildResult.StdErr.Should().Be(string.Empty);
            if (analysisLevel == null)
            {
                buildCommand.GetValues().Should().BeEmpty();
            }
            else
            {
                var computedEffectiveAnalysisLevel = buildCommand.GetValues()[0];
                computedEffectiveAnalysisLevel.Should().Be(analysisLevel.ToString());
            }
            buildResult.StdErr.Should().Be(string.Empty);
        }

        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.NextTargetFrameworkVersion)]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_defaults_preview_AnalysisLevel_to_the_next_tfm(string currentTFM, string nextTFMVersionNumber)
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = currentTFM,
                IsExe = true,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                }
                            }
                        }
                    ",
                },
            };
            testProject.AdditionalProperties.Add("AnalysisLevel", "preview");

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelPreviewConsoleApp" + currentTFM, targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                currentTFM, "EffectiveAnalysisLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();

            buildResult.StdErr.Should().Be(string.Empty, "If this test fails when updating to a new TFM, you need to update _PreviewAnalysisLevel and _LatestAnalysisLevel in Microsoft.NET.SDK.Analyzers.Targets");
            var computedEffectiveAnalysisLevel = buildCommand.GetValues()[0];
            computedEffectiveAnalysisLevel.Should().Be(nextTFMVersionNumber.ToString());
        }

        [InlineData("preview")]
        [InlineData("latest")]
        [InlineData("none")]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_resolves_all_nonnumeric_AnalysisLevel_strings(string analysisLevel)
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                }
                            }
                        }
                    ",
                },
            };
            testProject.AdditionalProperties.Add("AnalysisLevel", analysisLevel);

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelPreviewConsoleApp" + ToolsetInfo.CurrentTargetFramework + analysisLevel, targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                ToolsetInfo.CurrentTargetFramework, "EffectiveAnalysisLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();

            buildResult.StdErr.Should().Be(string.Empty);
            var computedEffectiveAnalysisLevel = buildCommand.GetValues()[0];
            computedEffectiveAnalysisLevel.Should().NotBe(analysisLevel);
        }

        [InlineData("latest", "all", "false")]
        [InlineData("latest", "", "true")]
        [InlineData("5", "", "true")]
        [InlineData("5.0", "minimum", "false")]
        [InlineData("6", "recommended", "false")]
        [InlineData("6.0", "", "true")]
        [InlineData("7", "none", "true")]
        [InlineData("7.0", "", "false")]
        [InlineData("8", "default", "false")]
        [InlineData("8.0", "", "true")]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_maps_analysis_properties_to_globalconfig(string analysisLevel, string analysisMode, string codeAnalysisTreatWarningsAsErrors)
        {
            // NOTE: This test will fail for "latest" analysisLevel when the "_LatestAnalysisLevel" property
            // is bumped in Microsoft.NET.Sdk.Analyzers.targets without a corresponding change in dotnet/roslyn-analyzers
            // repo that generates and maps to the globalconfig. This is an important regression test to ensure the
            // "latest" analysisLevel setting keeps working as expected when moving to a newer version of the .NET SDK.
            // Following changes are needed to ensure the failing test scenario passes again:
            //  1. In dotnet/roslyn-analyzers repo:
            //     a. Update "src/NetAnalyzers/Core/AnalyzerReleases.Shipped.md"to create a new release
            //        for the prior "_LatestAnalysisLevel" value and move all the entries from
            //        "src/NetAnalyzers/Core/AnalyzerReleases.Unshipped.md" to the shipped file.
            //        For example, see https://github.com/dotnet/roslyn-analyzers/pull/6246.
            //  2. In dotnet/sdk repo:
            //     a. Consume the new Microsoft.CodeAnalysis.NetAnalyzers package with the above sha.

            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                }
                            }
                        }
                    ",
                },
            };

            var mergedAnalysisLevel = !string.IsNullOrEmpty(analysisMode)
                ? $"{analysisLevel}-{analysisMode}"
                : analysisLevel;
            testProject.AdditionalProperties.Add("AnalysisLevel", mergedAnalysisLevel);
            testProject.AdditionalProperties.Add("CodeAnalysisTreatWarningsAsErrors", codeAnalysisTreatWarningsAsErrors);

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelPreviewConsoleApp" + ToolsetInfo.CurrentTargetFramework + analysisLevel, targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                ToolsetInfo.CurrentTargetFramework, "EffectiveAnalysisLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();

            buildResult.StdErr.Should().Be(string.Empty);
            var effectiveAnalysisLevel = buildCommand.GetValues()[0];
            if (effectiveAnalysisLevel.EndsWith(".0"))
                effectiveAnalysisLevel = effectiveAnalysisLevel.Substring(0, effectiveAnalysisLevel.Length - 2);
            var effectiveAnalysisMode = !string.IsNullOrEmpty(analysisMode) ? analysisMode : "default";
            var codeAnalysisTreatWarningsAsErrorsSuffix = codeAnalysisTreatWarningsAsErrors == "true" ? "_warnaserror" : string.Empty;
            var expectedMappedAnalyzerConfig = $"analysislevel_{effectiveAnalysisLevel}_{effectiveAnalysisMode}{codeAnalysisTreatWarningsAsErrorsSuffix}.globalconfig";

            buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                ToolsetInfo.CurrentTargetFramework,
                "EditorConfigFiles",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Build"
            };
            buildResult = buildCommand.Execute();

            buildResult.StdErr.Should().Be(string.Empty);
            var analyzerConfigFiles = buildCommand.GetValues().Select(Path.GetFileName);
            Assert.Single(analyzerConfigFiles.Where(file => string.Equals(file, expectedMappedAnalyzerConfig)));
        }
    }
}
