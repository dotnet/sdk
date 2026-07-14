// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToFloatWarningLevels : SdkTest
    {
        private const string targetFrameworkNet6 = "net6.0";
        private const string targetFrameworkNetFramework472 = "net472";

        [DataRow(targetFrameworkNet6, "6")]
        [DataRow(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkVersion)]
        [DataRow(targetFrameworkNetFramework472, "4")]
        [TestMethod]
        [RequiresMSBuildVersion("16.8")]
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

            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "warningLevelConsoleApp" + tfm);

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

        [DataRow(1, "1")]
        [DataRow(null, ToolsetInfo.CurrentTargetFrameworkVersion)]
        [TestMethod]
        [RequiresMSBuildVersion("16.8")]
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
            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "customWarningLevelConsoleApp");

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

        [DataRow(targetFrameworkNet6, "6.0")]
        [DataRow(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkVersion)]
        [DataRow(targetFrameworkNetFramework472, null)]
        [TestMethod]
        [RequiresMSBuildVersion("16.8")]
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

            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelConsoleApp" + tfm);

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

        [DataRow(ToolsetInfo.CurrentTargetFramework)]
        // Fixing this test requires bumping _LatestAnalysisLevel and _PreviewAnalysisLevel
        // Bumping will cause It_maps_analysis_properties_to_globalconfig to fail which requires changes in dotnet/roslyn-analyzers repo.
        // See instructions in the comment in It_maps_analysis_properties_to_globalconfig
        [TestMethod]
        [RequiresMSBuildVersion("16.8")]
        public void It_defaults_preview_AnalysisLevel_to_the_next_tfm(string currentTFM)
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

            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelPreviewConsoleApp" + currentTFM);

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
            // Verify that preview resolves to a numeric version (not the literal "preview" string)
            computedEffectiveAnalysisLevel.Should().NotBe("preview", "AnalysisLevel=preview should resolve to a numeric version");
            double.TryParse(computedEffectiveAnalysisLevel, out var previewLevel).Should().BeTrue(
                $"AnalysisLevel=preview should resolve to a numeric version, but got '{computedEffectiveAnalysisLevel}'");
            // Preview should be greater than or equal to the current TFM version
            var currentVersion = double.Parse(ToolsetInfo.CurrentTargetFrameworkVersion);
            previewLevel.Should().BeGreaterThanOrEqualTo(currentVersion,
                "AnalysisLevel=preview should resolve to a version >= the current TFM version");
        }

        [TestMethod]
        [RequiresMSBuildVersion("16.8")]
        public void It_has_globalconfig_for_latest_analysis_level()
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
            testProject.AdditionalProperties.Add("AnalysisLevel", "latest");

            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "latestAnalysisLevelGlobalConfig");

            // Verify that "latest" resolves to a numeric analysis level and has a corresponding globalconfig.
            // Note: During development of a new TFM (e.g., net11.0), _LatestAnalysisLevel may still point to
            // the prior shipped version (e.g., 10.0) until the new analyzers are shipped. This is expected.
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
            effectiveAnalysisLevel.Should().NotBe("latest", "AnalysisLevel=latest should resolve to a numeric version");
            double.TryParse(effectiveAnalysisLevel, out _).Should().BeTrue(
                $"AnalysisLevel=latest should resolve to a numeric version, but got '{effectiveAnalysisLevel}'");

            // Verify the corresponding globalconfig file exists
            var expectedGlobalConfig = $"analysislevel_{effectiveAnalysisLevel.Replace(".0", "")}_default.globalconfig";

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
            var analyzerConfigFiles = buildCommand.GetValues();
            var matchingConfigs = analyzerConfigFiles.Where(file => Path.GetFileName(file).Equals(expectedGlobalConfig, StringComparison.OrdinalIgnoreCase));
            matchingConfigs.Should().ContainSingle(
                $"""
                Expected to find globalconfig '{expectedGlobalConfig}' for AnalysisLevel=latest.

                To fix this test failure:
                  (1) Update the AnalyzerReleases files:
                      - Edit 'src/Microsoft.CodeAnalysis.NetAnalyzers/src/Microsoft.CodeAnalysis.NetAnalyzers/AnalyzerReleases.Shipped.md'
                        to create a new release section for the prior analysis level version (e.g., '## Release 10.0').
                      - Move all entries from 'AnalyzerReleases.Unshipped.md' to the new release section.
                      - Repeat for C#/VB.NET specific files if they have unshipped entries.
                  (2) Update _LatestAnalysisLevel and _PreviewAnalysisLevel in
                      'src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.Analyzers.targets'.
                  (3) Rebuild the SDK to regenerate the globalconfig files.
                """);

            var globalConfigPath = matchingConfigs.Single();
            File.Exists(globalConfigPath).Should().BeTrue(
                $"The globalconfig file '{expectedGlobalConfig}' should exist on disk.");
        }

        [DataRow("preview")]
        [DataRow("latest")]
        [DataRow("none")]
        [TestMethod]
        [RequiresMSBuildVersion("16.8")]
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

            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelPreviewConsoleApp" + ToolsetInfo.CurrentTargetFramework + analysisLevel);

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

        [DataRow("latest", "all", "false", "")]
        [DataRow("latest", "", "true", "")]
        [DataRow("latest", "all", "false", "Design")]
        [DataRow("latest", "", "true", "Documentation")]
        [DataRow("5", "", "true", "")]
        [DataRow("5.0", "minimum", "false", "")]
        [DataRow("5", "", "true", "Globalization")]
        [DataRow("5.0", "minimum", "false", "Interoperability")]
        [DataRow("6", "recommended", "false", "")]
        [DataRow("6.0", "", "true", "")]
        [DataRow("6", "recommended", "false", "Maintainability")]
        [DataRow("6.0", "", "true", "Naming")]
        [DataRow("7", "none", "true", "")]
        [DataRow("7.0", "", "false", "")]
        [DataRow("7", "none", "true", "Performance")]
        [DataRow("7.0", "", "false", "Reliability")]
        [DataRow("8", "default", "false", "")]
        [DataRow("8.0", "", "true", "")]
        [DataRow("8", "default", "false", "Security")]
        [DataRow("8.0", "", "true", "Usage")]
        [DataRow("9", "default", "false", "")]
        [DataRow("9.0", "", "true", "")]
        [DataRow("9", "default", "false", "Security")]
        [DataRow("9.0", "", "true", "Usage")]
        [DataRow("10", "default", "false", "")]
        [DataRow("10.0", "", "true", "")]
        [DataRow("10", "default", "false", "Security")]
        [DataRow("10.0", "", "true", "Usage")]
        [DataRow("11", "default", "false", "")]
        [DataRow("11.0", "", "true", "")]
        [DataRow("11", "default", "false", "Security")]
        [DataRow("11.0", "", "true", "Usage")]
        [TestMethod]
        [RequiresMSBuildVersion("16.8")]
        public void It_maps_analysis_properties_to_globalconfig(string analysisLevel, string analysisMode, string codeAnalysisTreatWarningsAsErrors, string category)
        {
            // Documentation: https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#code-analysis-properties
            //
            // NOTE: This test will fail for "latest" analysisLevel when the "_LatestAnalysisLevel" property
            // is bumped in Microsoft.NET.Sdk.Analyzers.targets without a corresponding change in the the analyzers
            // source in this repo that generates and maps to the globalconfig. This is an important regression test to ensure the
            // "latest" analysisLevel setting keeps working as expected when moving to a newer version of the .NET SDK.
            //
            // See the It_has_globalconfig_for_latest_analysis_level test for more explicit validation and detailed
            // instructions on what changes are needed when bumping to a new TFM.

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

            var analysisLevelPropertyName = "AnalysisLevel";
            var effectiveAnalysisLevelPropertyName = "EffectiveAnalysisLevel";
            if (!string.IsNullOrEmpty(category))
            {
                analysisLevelPropertyName += category;
                effectiveAnalysisLevelPropertyName += category;
            }

            var mergedAnalysisLevel = !string.IsNullOrEmpty(analysisMode)
                ? $"{analysisLevel}-{analysisMode}"
                : analysisLevel;
            testProject.AdditionalProperties.Add(analysisLevelPropertyName, mergedAnalysisLevel);
            testProject.AdditionalProperties.Add("CodeAnalysisTreatWarningsAsErrors", codeAnalysisTreatWarningsAsErrors);

            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelPreviewConsoleApp" + ToolsetInfo.CurrentTargetFramework + analysisLevel + category);

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                ToolsetInfo.CurrentTargetFramework, effectiveAnalysisLevelPropertyName)
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
            var expectedMappedAnalyzerConfig = $"analysislevel{category.ToLowerInvariant()}_{effectiveAnalysisLevel}_{effectiveAnalysisMode}{codeAnalysisTreatWarningsAsErrorsSuffix}.globalconfig";

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
            var analyzerConfigFiles = buildCommand.GetValues();
            var expectedAnalyzerConfigFiles = analyzerConfigFiles.Where(file => string.Equals(Path.GetFileName(file), expectedMappedAnalyzerConfig));
            var expectedAnalyzerConfigFile = Assert.ContainsSingle(expectedAnalyzerConfigFiles);
            File.Exists(expectedAnalyzerConfigFile).Should().BeTrue();
        }

        [DataRow("none", "false", new string[] { })]
        [DataRow("none", "true", new string[] { })]
        [DataRow("default", "false", new string[] { "CA2200" })]
        [DataRow("default", "true", new string[] { "CA2200" })]
        [DataRow("minimum", "false", new string[] { "CA1068", "CA2200" })]
        [DataRow("minimum", "true", new string[] { "CA1068", "CA2200" })]
        [DataRow("recommended", "false", new string[] { "CA1310", "CA1068", "CA2200" })]
        [DataRow("recommended", "true", new string[] { "CA1310", "CA1068", "CA2200" })]
        [DataRow("all", "false", new string[] { "CA1031", "CA1310", "CA1068", "CA2200" })]
        [DataRow("all", "true", new string[] { "CA1031", "CA1310", "CA1068", "CA2200" })]
        [TestMethod]
        [RequiresMSBuildVersion("17.12.0")]
        public void It_bulk_configures_rules_with_different_analysis_modes(string analysisMode, string codeAnalysisTreatWarningsAsErrors, string[] expectedViolations)
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
                        using System.Threading;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                }

                                // CA2200: Rethrow to preserve stack details
                                // Enabled by default as a build warning.
                                public static void CA2200_Default()
                                {
                                    try
                                    {
                                    }
                                    catch (ArithmeticException e)
                                    {
                                        throw e;
                                    }
                                }

                                // CA1068: CancellationToken parameters must come last
                                // Escalated to a build warning in 'minimum' or greater analysis modes.
                                public static void CA1068_Minimum(CancellationToken p1, int p2)
                                {
                                }

                                // CA1310: Specify StringComparison for correctness
                                // Escalated to a build warning in 'recommended' or greater analysis modes.
                                public static bool CA1310_Recommended(string s)
                                {
                                    return s.EndsWith(""end"");
                                }

                                // CA1031: Do not catch general exception types
                                // Escalated to a build warning only in 'all' analysis mode.
                                public static void CA1031_All()
                                {
                                    try
                                    {
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }
                        }
                    ",
                },
            };

            var analysisLevel = $"8-{analysisMode}";
            testProject.AdditionalProperties.Add("AnalysisLevel", analysisLevel);
            testProject.AdditionalProperties.Add("CodeAnalysisTreatWarningsAsErrors", codeAnalysisTreatWarningsAsErrors);

            // Don't emit a warning or an error when generators/analyzers can't be loaded.
            // This can occur when running tests against FullFramework MSBuild
            // if the build machine has an MSBuild install with an older version of Roslyn
            // than the generators in the SDK reference. We aren't testing the generators here
            // and this failure will occur more clearly in other places when it's
            // actually an important failure, so don't error out here.
            testProject.AdditionalProperties.Add("NoWarn", "CS9057");

            var testAsset = TestAssetsManager
                .CreateTestProject(testProject, identifier: "analysisLevelConsoleApp" + ToolsetInfo.CurrentTargetFramework + analysisLevel + $"Warnaserror:{codeAnalysisTreatWarningsAsErrors}");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var buildResult = buildCommand.Execute();

            var expectedToPass = analysisMode == "none" || codeAnalysisTreatWarningsAsErrors != "true";
            if (expectedToPass)
            {
                buildResult.Should().Pass();
            }
            else
            {
                buildResult.Should().Fail();
            }

            var violationPrefix = codeAnalysisTreatWarningsAsErrors == "true" ? "error" : "warning";
            expectedViolations = expectedViolations.Select(id => $"{violationPrefix} {id}").ToArray();
            if (expectedViolations.Length == 0)
            {
                buildResult.StdOut.Should().NotContainAll(new[] { "error", "warning" });
            }
            else
            {
                buildResult.StdOut.Should().ContainAll(expectedViolations);
            }
        }
    }
}
