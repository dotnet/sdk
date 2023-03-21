// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Castle.Core.Internal;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class TemplateCreatorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplateCreatorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        private const string TemplateConfigBooleanParam = /*lang=json,strict*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": {
                    "paramA": {
                      "type": "parameter",
                      "datatype": "bool"
                    }
                }
            }
            """;

        private const string? XmlConditionWithinMsBuildConditionSource = """
            <Project>
                <ItemGroup Condition="'$(paramA)' == 'True'">
                    <!-- X -->
                    <!--#if (paramA)-->
                    <Item name="a" />
                    <!--#endif-->
                    <!--#if (!paramA)-->
                    <Item name="b" />
                    <!--#endif-->
                </ItemGroup>
            </Project>
            """;

        private const string XmlConditionWithinMsBuildConditionOutputOnFalse = """
            <Project>
            </Project>
            """;

        private const string XmlConditionWithinMsBuildConditionOutputOnTrue = """
            <Project>
                <ItemGroup>
                    <!-- X -->
                    <Item name="a" />
                </ItemGroup>
            </Project>
            """;

        private const string? MsBuildConditionWithinXmlConditionSource = """
            <Project>
                <!-- X -->
                <!--#if (paramA)-->
                <ItemGroup Condition="'$(paramA)' == 'True'">
                    <Item name="a" />
                </ItemGroup>
                <ItemGroup Condition="'$(paramA)' == 'False'">
                    <Item name="b" />
                </ItemGroup>
                <!--#endif-->
            </Project>
            """;

        private const string MsBuildConditionWithinXmlConditionOutputOnFalse = """
            <Project>
                <!-- X -->
            </Project>
            """;

        private const string MsBuildConditionWithinXmlConditionOutputOnTrue = """
            <Project>
                <!-- X -->
                <ItemGroup>
                    <Item name="a" />
                </ItemGroup>
            </Project>
            """;

        [Theory]
        [InlineData(false, XmlConditionWithinMsBuildConditionSource, XmlConditionWithinMsBuildConditionOutputOnFalse)]
        [InlineData(true, XmlConditionWithinMsBuildConditionSource, XmlConditionWithinMsBuildConditionOutputOnTrue)]
        [InlineData(false, MsBuildConditionWithinXmlConditionSource, MsBuildConditionWithinXmlConditionOutputOnFalse)]
        [InlineData(true, MsBuildConditionWithinXmlConditionSource, MsBuildConditionWithinXmlConditionOutputOnTrue)]
        public async void InstantiateAsync_XmlConditionsAndComments(bool paramA, string sourceSnippet, string expectedOutput)
        {
            IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            {
                { "paramA", paramA.ToString() }
            };

            await InstantiateAsyncHelper(
                TemplateConfigBooleanParam,
                sourceSnippet,
                expectedOutput,
                string.Empty,
                false,
                sourceExtension: ".csproj",
                expectedOutputName: "./sourceFile.csproj",
                parameters1: parameters);
        }

        private const string TemplateConfigQuotelessLiteralsEnabled = /*lang=json,strict*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": {
                    "ChoiceParam": {
                      "type": "parameter",
                      "description": "sample switch",
                      "datatype": "choice",
                      "enableQuotelessLiterals": true,
                      "choices": [
                        {
                          "choice": "FirstChoice",
                          "description": "First Sample Choice"
                        },
                        {
                          "choice": "SecondChoice",
                          "description": "Second Sample Choice"
                        },
                        {
                          "choice": "ThirdChoice",
                          "description": "Third Sample Choice"
                        }
                      ],
                      "defaultValue": "ThirdChoice",
                      "defaultIfOptionWithoutValue": "SecondChoice"
                    }
                }
            }
            """;

        [Theory]
        // basic choice
        [InlineData("FirstChoice", "FIRST", false)]
        // nonexistent choice
        [InlineData("Invalid", "UNKNOWN", true)]
        // value not set - default used
        [InlineData(null, "SECOND", false)]
        // explicit unset
        [InlineData("", "UNKNOWN", false)]
        public async void InstantiateAsync_ParamsProperlyHonored(string? parameterValue, string expectedOutput, bool instantiateShouldFail)
        {
            string sourceSnippet = """
                #if( ChoiceParam == FirstChoice )
                FIRST
                #elseif (ChoiceParam == SecondChoice )
                SECOND
                #elseif (ChoiceParam == ThirdChoice )
                THIRD
                #else
                UNKNOWN
                #endif
                """;
            IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            {
                { "ChoiceParam", parameterValue }
            };

            await InstantiateAsyncHelper(
                TemplateConfigQuotelessLiteralsEnabled,
                sourceSnippet,
                expectedOutput,
                "ChoiceParam",
                instantiateShouldFail,
                parameters1: parameters);
        }

        private const string TemplateConfigCyclicParamsDependency = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": {
                    "A": {
                      "type": "parameter",
                      "datatype": "bool",
                      "isEnabled": "!C && B != false",
                    },
                    "B": {
                      "type": "parameter",
                      "datatype": "bool",
                      "isEnabled": "A != true || C",
                    },
                    "C": {
                      "type": "parameter",
                      "datatype": "bool"
                    },
                }
            }
            """;

        [Theory]
        [InlineData(false, true, false, "B,", false)]
        [InlineData(true, false, false, "A,", true)]
        // Theoretically the result is deterministic, but we'd need to understand the expression tree as well (and purge it)
        [InlineData(true, false, true, "B,C", true)]
        public async void InstantiateAsync_ConditionalParametersCycleEvaluation(bool a_val, bool b_val, bool c_val, string expectedOutput, bool instantiateShouldFail)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = """
                #if( A )
                A,
                #endif

                #if( B )
                B,
                #endif

                #if( C )
                C
                #endif
                """;
            IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            {
                { "A", a_val.ToString() },
                { "B", b_val.ToString() },
                { "C", c_val.ToString() }
            };

            await InstantiateAsyncHelper(
                TemplateConfigCyclicParamsDependency,
                sourceSnippet,
                expectedOutput,
                @"Failed to create template.
Details: Parameter conditions contain cyclic dependency: [A, B, A] that is preventing deterministic evaluation.",
                instantiateShouldFail,
                parameters1: parameters);
        }

        private const string TemplateConfigIsRequiredCondition = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": {
                    "A": {
                      "type": "parameter",
                      "datatype": "bool",
                      "isRequired": "!C && B != false",
                      "defaultValue": "false",
                    },
                    "B": {
                      "type": "parameter",
                      "datatype": "bool",
                      "isRequired": "A != true || C",
                      "defaultValue": "true",
                    },
                    "C": {
                      "type": "parameter",
                      "datatype": "bool",
                      "defaultValue": "false",
                    },
                }
            }
            """;

        [Theory]
        [InlineData(false, true, false, "B,", false, null)]
        [InlineData(true, false, false, "A,", false, null)]
        [InlineData(null, null, true, "C", true, "B")]
        [InlineData(null, true, false, "C", true, "A")]
        [InlineData(null, null, false, "C", true, "A, B")]
        [InlineData(null, null, null, "B,", true, "A, B")]
        [InlineData(null, true, null, "B,", true, "A")]
        [InlineData(null, false, null, "", false, null)]
        [InlineData(false, false, false, "", false, null)]
        [InlineData(true, false, true, "A,C", false, null)]
        public async void InstantiateAsync_ConditionalParametersIsRequiredEvaluation(bool? a_val, bool? b_val, bool? c_val, string expectedOutput, bool instantiateShouldFail, string expectedErrorMessage)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = """
                #if( A )
                A,
                #endif

                #if( B )
                B,
                #endif

                #if( C )
                C
                #endif
                """;
            IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            {
                { "A", a_val?.ToString() },
                { "B", b_val?.ToString() },
                { "C", c_val?.ToString() }
            }
                .Where(p => p.Value != null)
                .ToDictionary(p => p.Key, p => p.Value);

            await InstantiateAsyncHelper(
                TemplateConfigIsRequiredCondition,
                sourceSnippet,
                // To make the test data more compact we have left out the newlines - let's add them back here
                expectedOutput.Length <= 2 ? expectedOutput : expectedOutput.Replace(",", $",{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}"),
                expectedErrorMessage,
                instantiateShouldFail,
                parameters1: parameters);
        }

        private const string TemplateConfigEnabledAndRequiredConditionsTogether = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": {
                    "A": {
                      "type": "parameter",
                      "datatype": "string",
                      "isEnabled": "A_enable",
                      "isRequired": "true",
                    },
                    "B": {
                      "type": "parameter",
                      "datatype": "string",
                      "isEnabled": "B_enable",
                      "isRequired": true
                    },
                    "A_enable": {
                      "type": "parameter",
                      "datatype": "bool"
                    },
                    "B_enable": {
                      "type": "parameter",
                      "datatype": "bool"
                    },
                }
            }
            """;

        [Theory]
        [InlineData(false, false, null, null, "", false, null)]
        [InlineData(true, false, null, null, "", true, "A")]
        [InlineData(true, false, null, "x", "", true, "A")]
        [InlineData(true, true, "false", "false", "", false, null)]
        [InlineData(true, false, "true", null, "A,", false, null)]
        [InlineData(false, true, null, null, "", true, "B")]
        [InlineData(true, true, null, null, "", true, "A, B")]
        [InlineData(null, null, null, null, "", false, null)]
        [InlineData(null, true, null, "true", "B,", false, null)]
        [InlineData(true, null, "true", "false", "A,", false, null)]
        [InlineData(true, null, "true", null, "A,", false, null)]
        [InlineData(null, true, null, null, "", true, "B")]
        public async void InstantiateAsync_ConditionalParametersRequiredOverwrittenByDisabled(
            bool? a_enable_val,
            bool? b_enable_val,
            string? a,
            string? b,
            string expectedOutput,
            bool instantiateShouldFail,
            string expectedErrorMessage)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = """
                #if( A )
                A,
                #endif

                #if( B )
                B,
                #endif

                #if( C )
                C
                #endif
                """;

            IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            {
                { "A_enable", a_enable_val?.ToString() },
                { "B_enable", b_enable_val?.ToString() },
                { "A", a },
                { "B", b }
            }
                .Where(p => p.Value != null)
                .ToDictionary(p => p.Key, p => p.Value);

            await InstantiateAsyncHelper(
                TemplateConfigEnabledAndRequiredConditionsTogether,
                sourceSnippet,
                expectedOutput,
                expectedErrorMessage,
                instantiateShouldFail,
                parameters1: parameters);
        }

        private const string TemplateConfigEnabledConditionInversed = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": { 
                    "A": {
                      "type": "parameter",
                      "datatype": "bool",
                      "isEnabled": "!A_disable",
                      "isRequired": false
                    }, 
                    "A_disable": {
                      "type": "parameter",
                      "datatype": "bool"
                    },
                }
            }
            """;

        [Theory]
        [InlineData(false, false, "", false, null)]
        [InlineData(false, true, "A,", false, null)]
        [InlineData(false, null, "", false, null)]
        [InlineData(true, false, "", false, null)]
        [InlineData(true, true, "", false, null)]
        [InlineData(null, false, "", false, null)]
        [InlineData(null, true, "A,", false, null)]
        public async void InstantiateAsync_ConditionalParametersInversedEnablingCondition(
            bool? a_disable_val,
            bool? a,
            string expectedOutput,
            bool instantiateShouldFail,
            string expectedErrorMessage)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = """
                #if( A )
                A,
                #endif

                #if( B )
                B,
                #endif 
                """;

            IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            {
                { "A_disable", a_disable_val?.ToString() },
                { "A", a?.ToString() },
            }
                .Where(p => p.Value != null)
                .ToDictionary(p => p.Key, p => p.Value);

            await InstantiateAsyncHelper(
                TemplateConfigEnabledConditionInversed,
                sourceSnippet,
                expectedOutput,
                expectedErrorMessage,
                instantiateShouldFail,
                parameters1: parameters);
        }

        private const string TemplateConfigEnabledConditionEvaluationBehavior = """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": { 
                    "A": {
                      "type": "parameter",
                      "datatype": "bool", 
                      "isRequired": false,
                      "isEnabled": "!IsDisabled",
                      "defaultValue": true
                    },
                    ##Enable_Param 
                }
            }
            """;

        private const string ParamSnippetStringNoDefault = """
                "IsDisabled": {
                  "type": "parameter",
                  "datatype": "string" 
                }
            """;

        private const string ParamSnippetStringDefault = """
                "IsDisabled": {
                  "type": "parameter",
                  "datatype": "string",
                  "defaultValue": "false"
                }
            """;

        private const string ParamSnippetBooleanNoDefault = """
                "IsDisabled": {
                  "type": "parameter",
                  "datatype": "bool" 
                }
            """;

        private const string ParamSnippetBooleanDefaultFalse = """
                "IsDisabled": {
                  "type": "parameter",
                  "datatype": "bool",
                  "defaultValue": false
                }
            """;

        private const string ParamSnippetBooleanDefaultTrue = """
                "IsDisabled": {
                  "type": "parameter",
                  "datatype": "bool",
                  "defaultValue": true
                }
            """;

        [Theory]
        [InlineData(ParamSnippetStringNoDefault, "", true, "Failed to evaluate condition IsEnabled on parameter A (condition text: !IsDisabled, evaluation error: Unable to logical not System.String)")]
        [InlineData(ParamSnippetStringDefault, "A,", false, null)]
        [InlineData(ParamSnippetBooleanNoDefault, "A,", false, null)]
        [InlineData(ParamSnippetBooleanDefaultFalse, "A,", false, null)]
        [InlineData(ParamSnippetBooleanDefaultTrue, "notA,", false, null)]
        //[InlineData(false, true, "A,", false, null)]
        public async void InstantiateAsync_ConditionalParametersEvaluationBehavior(
            string paramSnippet,
            string expectedOutput,
            bool instantiateShouldFail,
            string expectedErrorMessage)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = """
                #if (A)
                A,
                #endif

                #if (!A)
                notA,
                #endif

                #if( B )
                B,
                #endif 
                """;

            //IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            //{
            //    { "A", a_disable_val?.ToString() },
            //    { "Ap", a?.ToString() },
            //}
            //    .Where(p => p.Value != null)
            //    .ToDictionary(p => p.Key, p => p.Value);

            await InstantiateAsyncHelper(
                TemplateConfigEnabledConditionEvaluationBehavior.Replace("##Enable_Param", paramSnippet),
                sourceSnippet,
                expectedOutput,
                expectedErrorMessage,
                instantiateShouldFail,
                parameters1: new Dictionary<string, string?>());
        }

        private const string TemplateConfigForExternalConditionsEvaluation = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "symbols": {
                    "parA": {
                      "type": "parameter",
                      "datatype": "string",
                      "isEnabled": "A_enable",
                      "isRequired": "true || true",
                    },
                    "parB": {
                      "type": "parameter",
                      "datatype": "string",
                      "isEnabled": "B_enable",
                      "isRequired": true
                    },
                    "C": {
                      "type": "parameter",
                      "datatype": "bool"
                    },
                    "A_enable": {
                      "type": "parameter",
                      "datatype": "bool"
                    },
                    "B_enable": {
                      "type": "parameter",
                      "datatype": "bool"
                    },
                }
            }
            """;

        [Theory]
        [InlineData(null, true, false, null, false, null, /*c_val*/ true, "C", false, "")]
        [InlineData(true, true, true, null, false, null, /*c_val*/ false, "parA,", false, "")]
        [InlineData(null, true, false, null, true, null, /*c_val*/ true, "", true, "parB")]
        [InlineData(null, true, true, null, false, null, /*c_val*/ true, "", true, "parA")]
        [InlineData(null, true, false, null, false, false, /*c_val*/ false, "", true, @"Attempt to pass result of external evaluation of parameters conditions for parameter(s) that do not have appropriate condition set in template (IsEnabled or IsRequired attributes not populated with condition): B (parameter)")]
        public async void InstantiateAsync_ConditionalParametersWithExternalEvaluation(
            bool? a_val,
            bool? a_enabled,
            bool? a_required,
            bool? b_val,
            bool? b_enabled,
            bool? b_required,
            bool c_val,
            string expectedOutput,
            bool instantiateShouldFail,
            string expectedErrorMessage)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = """
                #if( parA )
                parA,
                #endif

                #if( parB )
                parB,
                #endif

                #if( C )
                C
                #endif
                """;

            List<InputDataBag> parameters = new(
                new[]
                {
                    new InputDataBag("parA", a_val, a_enabled, a_required),
                    new InputDataBag("parB", b_val, b_enabled, b_required),
                    new InputDataBag("C", c_val),
                });

            await InstantiateAsyncHelper(
                TemplateConfigForExternalConditionsEvaluation,
                sourceSnippet,
                expectedOutput,
                expectedErrorMessage,
                instantiateShouldFail,
                parameters2: parameters);
        }

        private const string TemplateConfigPreferDefaultNameWithDefaultName = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "preferDefaultName": true,
                "defaultName": "defaultName",
                "sourceName": "sourceFile"
            }
            """;

        private const string TemplateConfigPreferDefaultNameWithoutDefaultName = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "preferDefaultName": true,
                "sourceName": "sourceFile"
            }
            """;

        private const string TemplateConfigNoPreferDefaultNameWithDefaultName = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "tst",
                "shortName": "tst",
                "preferDefaultName": false,
                "sourceName": "sourceFile"
            }
            """;

        [Theory]
        [InlineData(TemplateConfigPreferDefaultNameWithDefaultName, "thisIsAName", "./thisIsAName.cs", false, "")]
        [InlineData(TemplateConfigPreferDefaultNameWithDefaultName, null, "./defaultName.cs", false, "")]
        [InlineData(TemplateConfigNoPreferDefaultNameWithDefaultName, null, "./tst2.cs", false, "")]
        [InlineData(TemplateConfigPreferDefaultNameWithoutDefaultName, null, "./tst2.cs", true, "Failed to create template: the template name is not specified. Template configuration does not configure a default name that can be used when name is not specified. Specify the name for the template when instantiating or configure a default name in the template configuration.")]
        public async void InstantiateAsync_PreferDefaultName(string templateConfig, string? name, string expectedOutputName, bool instanceFailure, string errorMessage)
        {
            string sourceSnippet = """
                using System;

                Console.log("Hello there, this is a test!");
                """;

            await InstantiateAsyncHelper(
                templateConfig,
                sourceSnippet,
                sourceSnippet,
                errorMessage,
                instanceFailure,
                name: name,
                expectedOutputName: expectedOutputName);
        }

        [Fact]
        public async void InstantiateAsync_InvalidTemplate()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            const string templateConfig = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "test"
            }
            """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, templateConfig }
            };

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(environmentSettings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environmentSettings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new();

            IFile? templateConfigFile = sourceMountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using var runnableConfig = new RunnableProjectConfig(environmentSettings, rpg, templateConfigFile);

            TemplateCreator creator = new TemplateCreator(environmentSettings);

            string targetDir = environmentSettings.GetTempVirtualizedPath();

            ITemplateCreationResult instantiateResult = await creator.InstantiateAsync(
                    templateInfo: runnableConfig,
                    name: "test",
                    fallbackName: "test",
                    inputParameters: new Dictionary<string, string?>(),
                    outputPath: targetDir);

            Assert.Equal(CreationResultStatus.TemplateIssueDetected, instantiateResult.Status);
            Assert.Equal("The template is invalid and cannot be instantiated.", instantiateResult.ErrorMessage);

            string[] errors = loggedMessages.Where(m => m.Level == LogLevel.Error).Select(m => m.Message).ToArray();
            string debugMessage = loggedMessages.Where(m => m.Level == LogLevel.Debug).Select(m => m.Message).Last();

            Assert.Equal(2, errors.Length);

            Assert.Equal(
                """
                The template 'test' (test.template) has the following validation errors:
                   [Error][MV003] Missing 'shortName'.

                """,
                errors[0]);
            Assert.Equal("Failed to load the template 'test' (test.template): the template is not valid.", errors[1]);
            Assert.Equal(
            """
                The template 'test' (test.template) has the following validation messages:
                   [Info][MV005] Missing 'sourceName'.
                   [Info][MV006] Missing 'author'.
                   [Info][MV007] Missing 'groupIdentity'.
                   [Info][MV008] Missing 'generatorVersions'.
                   [Info][MV009] Missing 'precedence'.
                   [Info][MV010] Missing 'classifications'.

                """,
            debugMessage);
        }

        [Fact]
        public async void InstantiateAsync_InvalidLocalization()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            const string templateConfig = /*lang=json*/ """
            {
                "identity": "test.template",
                "name": "test",
                "shortName": "test"
            }
            """;

            //this localization has post action, which doesn't exist in main config
            const string templateLoc = /*lang=json*/ """
            {
                "name": "name_de-DE",
                "description": "desc_de-DE",
                "postActions/pa0/description": "pa0_desc_de-DE",
                "postActions/pa0/manualInstructions/first_instruction/text": "pa0_manualInstructions_de-DE"
            }
            """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, templateConfig },
                { ".template.config/localize/templatestrings.de-DE.json", templateLoc }
            };

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(environmentSettings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environmentSettings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new();

            IFile? templateConfigFile = sourceMountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);
            IFile? templateLocFile = sourceMountPoint.FileInfo(".template.config/localize/templatestrings.de-DE.json");
            Assert.NotNull(templateLoc);

            using var runnableConfig = new RunnableProjectConfig(environmentSettings, rpg, templateConfigFile, localeConfigFile: templateLocFile);

            TemplateCreator creator = new TemplateCreator(environmentSettings);

            string targetDir = environmentSettings.GetTempVirtualizedPath();

            ITemplateCreationResult instantiateResult = await creator.InstantiateAsync(
                    templateInfo: runnableConfig,
                    name: "test",
                    fallbackName: "test",
                    inputParameters: new Dictionary<string, string?>(),
                    outputPath: targetDir);

            Assert.Equal(CreationResultStatus.Success, instantiateResult.Status);
            Assert.Null(instantiateResult.ErrorMessage);

            string error = loggedMessages.Where(m => m.Level == LogLevel.Error).Select(m => m.Message).Single();
            string warning = loggedMessages.Where(m => m.Level == LogLevel.Warning).Select(m => m.Message).Single();

            Assert.Equal(
                """
                The template 'test' (test.template) has the following validation errors in 'de-DE' localization:
                   [Error][LOC002] Post action(s) with id(s) 'pa0' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.
                
                """,
                error);
            Assert.Equal("Failed to load the 'de-DE' localization the template 'test' (test.template): the localization file is not valid. The localization will be skipped.", warning);
        }

        private async Task InstantiateAsyncHelper(
            string templateSnippet,
            string sourceSnippet,
            string expectedOutput,
            string expectedErrorMessage,
            bool instantiateShouldFail,
            string? name = "sourceFile",
            string expectedOutputName = "./sourceFile.cs",
            string sourceExtension = ".cs",
            IReadOnlyDictionary<string, string?>? parameters1 = null,
            IReadOnlyList<InputDataBag>? parameters2 = null)
        {
            //
            // Template content preparation
            //

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, templateSnippet }
            };

            string sourceFileName = name is null ? "sourceFile" + sourceExtension : name + sourceExtension;

            //content
            templateSourceFiles.Add(sourceFileName, sourceSnippet);

            //
            // Dependencies preparation and mounting
            //

            string sourceBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(_engineEnvironmentSettings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = _engineEnvironmentSettings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new();

            IFile? templateConfigFile = sourceMountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using var runnableConfig = new RunnableProjectConfig(_engineEnvironmentSettings, rpg, templateConfigFile);

            TemplateCreator creator = new TemplateCreator(_engineEnvironmentSettings);

            string targetDir = _engineEnvironmentSettings.GetTempVirtualizedPath();

            ITemplateCreationResult res;
            if (parameters1 != null)
            {
                res = await creator.InstantiateAsync(
                    templateInfo: runnableConfig,
                    name: name,
                    fallbackName: "tst2",
                    inputParameters: parameters1!,
                    outputPath: targetDir);
            }
            else if (parameters2 != null)
            {
                IParameterDefinitionSet parameters = runnableConfig.ParameterDefinitions;

                InputDataSet data;
                try
                {
                    data = new InputDataSet(
                        parameters,
                        parameters2!.Select(p => new EvaluatedInputParameterData(
                            parameters[p.Name],
                            p.Value,
                            DataSource.User,
                            p.IsEnabledConditionResult,
                            p.IsRequiredConditionResult,
                            p.IsNull ? InputDataState.Unset : InputDataState.Set)).ToList())
                    {
                        ContinueOnMismatchedConditionsEvaluation = true
                    };

                    res = await creator.InstantiateAsync(
                        templateInfo: runnableConfig,
                        name: name,
                        fallbackName: "tst2",
                        inputParameters: data,
                        outputPath: targetDir);
                }
                catch (Exception e)
                {
                    Assert.True(instantiateShouldFail);
                    Assert.True(instantiateShouldFail);
                    e.Message.Should().BeEquivalentTo(e.Message);
                    return;
                }
            }
            else
            {
                InputDataSet parameters = new InputDataSet(runnableConfig);

                res = await creator.InstantiateAsync(
                    templateInfo: runnableConfig,
                    name: name,
                    fallbackName: "tst2",
                    inputParameters: parameters,
                    outputPath: targetDir);
            }

            if (instantiateShouldFail)
            {
                res.ErrorMessage.Should().NotBeNullOrEmpty();
                res.ErrorMessage.Should().Contain(expectedErrorMessage);
                res.OutputBaseDirectory.Should().Match(s =>
                    s.IsNullOrEmpty() || !_engineEnvironmentSettings.Host.FileSystem.FileExists(s));
            }
            else
            {
                res.ErrorMessage.Should().BeNull();
                res.OutputBaseDirectory.Should().NotBeNullOrEmpty();

                res.CreationEffects.Should().NotBeNull();
                res.CreationEffects!.FileChanges.Should().NotBeNullOrEmpty().And.HaveCount(1);
                res.CreationEffects.FileChanges[0].TargetRelativePath.Should().Be(expectedOutputName);

                string resultContent = File.Exists(Path.Combine(res.OutputBaseDirectory!, sourceFileName))
                    ? _engineEnvironmentSettings.Host.FileSystem
                    .ReadAllText(Path.Combine(res.OutputBaseDirectory!, sourceFileName)).Trim()
                    : _engineEnvironmentSettings.Host.FileSystem
                    .ReadAllText(Path.Combine(res.OutputBaseDirectory!, expectedOutputName)).Trim();
                resultContent.Should().BeEquivalentTo(expectedOutput.Trim());
            }
        }

        private class InputDataBag
        {
            public InputDataBag(string name, bool? value, bool? isEnabledConditionResult = null, bool? isRequiredConditionResult = null)
            {
                Name = name;
                Value = value?.ToString();
                IsEnabledConditionResult = isEnabledConditionResult;
                IsRequiredConditionResult = isRequiredConditionResult;
                IsNull = value == null;
            }

            public string Name { get; }

            public string? Value { get; }

            public bool? IsEnabledConditionResult { get; }

            public bool? IsRequiredConditionResult { get; }

            public bool IsNull { get; }
        }
    }
}
