// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class RunnableProjectGeneratorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public RunnableProjectGeneratorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async void CreateAsyncTest_GuidsMacroProcessingCaseSensitivity()
        {
            //
            // Template content preparation
            //

            Guid inputTestGuid = new Guid("12aa8f4e-a4aa-4ac1-927c-94cb99485ef1");
            string contentFileNamePrefix = "content - ";
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "test",
                ShortNameList = new[] { "test" },
                Guids = new List<Guid>()
                {
                    inputTestGuid
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, config.ToJsonString() }
            };

            //content
            foreach (string guidFormat in GuidMacroConfig.DefaultFormats.Select(c => c.ToString()))
            {
                templateSourceFiles.Add(contentFileNamePrefix + guidFormat, inputTestGuid.ToString(guidFormat));
            }

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = environment.GetTempVirtualizedPath();
            string targetDir = environment.GetTempVirtualizedPath();
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();

            TestFileSystemUtils.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environment.MountPath(sourceBasePath);
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, config, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            Guid expectedResultGuid = Guid.Empty;
            foreach (string guidFormat in GuidMacroConfig.DefaultFormats.Select(c => c.ToString()))
            {
                string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, contentFileNamePrefix + guidFormat));
                Assert.True(
                    Guid.TryParseExact(resultContent, guidFormat, out Guid resultGuid),
                    $"Expected the result conent ({resultContent}) to be parseable by Guid format '{guidFormat}'");

                if (expectedResultGuid == Guid.Empty)
                {
                    expectedResultGuid = resultGuid;
                }
                else
                {
                    Assert.Equal(expectedResultGuid, resultGuid);
                }
            }
            Assert.NotEqual(inputTestGuid, expectedResultGuid);
        }

        private const string TemplateConfigQuotelessLiteralsNotEnabled = /*lang=json*/ """
        {
            "identity": "test.template",
            "name": "test",
            "shortName": "test",
            "symbols": {
                "ChoiceParam": {
                  "type": "parameter",
                  "description": "sample switch",
                  "datatype": "choice",
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
                }
            }
        }
        """;

        private const string TemplateConfigQuotelessLiteralsEnabled = /*lang=json*/ """

            {
                "identity": "test.template",
                "name": "test",
                "shortName": "test",
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
                    }
                }
            }

            """;

        [Theory]
        [InlineData(TemplateConfigQuotelessLiteralsNotEnabled, "UNKNOWN")]
        [InlineData(TemplateConfigQuotelessLiteralsEnabled, "SECOND")]
        public async void CreateAsyncTest_ConditionWithUnquotedChoiceLiteral(string templateConfig, string expectedResult)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = """
                //#if( ChoiceParam == FirstChoice )
                FIRST
                //#elseif (ChoiceParam == SecondChoice )
                SECOND
                //#elseif (ChoiceParam == ThirdChoice )
                THIRD
                //#else
                UNKNOWN
                //#endif
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, templateConfig },
                //content
                { "sourcFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = environment.GetTempVirtualizedPath();
            string targetDir = environment.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environment.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromString(templateConfig);
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, string?>() { { "ChoiceParam", "SecondChoice" } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile")).Trim();
            Assert.Equal(expectedResult, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_MultiChoiceParamReplacingAndCondition()
        {
            //
            // Template content preparation
            //

            string templateConfig = /*lang=json*/ """
                {
                    "identity": "test.template",
                    "name": "test",
                    "shortName": "test",
                    "symbols": {    
                        "ChoiceParam": {
                          "type": "parameter",
                          "description": "sample switch",
                          "datatype": "choice",
                          "allowMultipleValues": true,
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
                          "replaces": "REPLACE_VALUE"
                        }
                    }
                }
                """;

            string sourceSnippet = """
                MultiChoiceValue: REPLACE_VALUE
                //#if( ChoiceParam == FirstChoice )
                FIRST
                //#endif
                //#if (ChoiceParam == SecondChoice )
                SECOND
                //#endif
                //#if (ChoiceParam == ThirdChoice )
                THIRD
                //#endif
                """;

            string expectedSnippet = """
                MultiChoiceValue: SecondChoice|ThirdChoice
                SECOND
                THIRD

                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, templateConfig },
                //content
                { "sourcFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = environment.GetTempVirtualizedPath();
            string targetDir = environment.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environment.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromString(templateConfig);
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, object?>() { { "ChoiceParam", new MultiValueParameter(new[] { "SecondChoice", "ThirdChoice" }) } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_MultiChoiceParamAndConditionMacro()
        {
            //
            // Template content preparation
            //

            string templateConfig = /*lang=json,strict*/ """
                {
                  "$schema": "https://json.schemastore.org/template.json",
                  "author": "Test Asset",
                  "classifications": [
                    "Test Asset"
                  ],
                  "name": "MultiSelect.Template",
                  "generatorVersions": "[1.0.0.0-*)",
                  "tags": {
                    "type": "project",
                    "language": "C#"
                  },
                  "groupIdentity": "MultiSelect.Template",
                  "precedence": "100",
                  "identity": "MultiSelect.Template",
                  "shortName": "MultiSelect.Template",
                  "sourceName": "bar",
                  "symbols": {
                    "Platform": {
                      "type": "parameter",
                      "description": "The target platform for the project.",
                      "datatype": "choice",
                      "allowMultipleValues": true,
                      "enableQuotelessLiterals": true,
                      "choices": [
                        {
                          "choice": "Windows",
                          "description": "Windows Desktop"
                        },
                        {
                          "choice": "WindowsPhone",
                          "description": "Windows Phone"
                        },
                        {
                          "choice": "MacOS",
                          "description": "Macintosh computers"
                        },
                        {
                          "choice": "iOS",
                          "description": "iOS mobile"
                        },
                        {
                          "choice": "android",
                          "description": "android mobile"
                        },
                        {
                          "choice": "nix",
                          "description": "Linux distributions"
                        }
                      ],
                      "defaultValue": "MacOS|iOS"
                    },
                    "IsMobile": {
                      "type": "computed",
                      "value": "((Platform == android || Platform == iOS || Platform == WindowsPhone) && Platform != Windows && Platform != MacOS && Platform != nix)"
                    },
                    "IsAndroidOnly": {
                      "type": "computed",
                      "value": "(Platform == android && Platform != iOS && Platform != WindowsPhone && Platform != Windows && Platform != MacOS && Platform != nix)"
                    },
                    "joinedRename": {
                      "type": "generated",
                      "generator": "join",
                      "replaces": "SupportedPlatforms",
                      "parameters": {
                        "symbols": [
                          {
                            "type": "ref",
                            "value": "Platform"
                          }
                        ],
                        "separator": ", ",
                        "removeEmptyValues": true
                      }
                    }
                  }
                }
                """;

            string sourceSnippet = """
                //#if IsAndroidOnly
                This renders for android only
                //#elseif IsMobile
                This renders for rest of mobile platforms
                //#else
                This renders for desktop platforms
                //#endif
                Console.WriteLine("Hello, World!");

                // Plats: SupportedPlatforms
                """;

            string expectedSnippet = """
                This renders for rest of mobile platforms
                Console.WriteLine("Hello, World!");

                // Plats: android, iOS
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, templateConfig },
                //content
                { "sourcFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = environment.GetTempVirtualizedPath();
            string targetDir = environment.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environment.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromString(templateConfig);
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, object?>() { { "Platform", new MultiValueParameter(new[] { "android", "iOS" }) } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_MultiChoiceParamJoining()
        {
            //
            // Template content preparation
            //

            string templateConfig = /*lang=json*/ """
                {
                  "identity": "test.template",
                  "name": "test",
                  "shortName": "test",
                  "symbols": {
                    "Platform": {
                      "type": "parameter",
                      "description": "The target framework for the project.",
                      "datatype": "choice",
                      "allowMultipleValues": true,
                      "choices": [
                        {
                          "choice": "Windows",
                          "description": "Windows Desktop"
                        },
                        {
                          "choice": "WindowsPhone",
                          "description": "Windows Phone"
                        },
                        {
                          "choice": "MacOS",
                          "description": "Macintosh computers"
                        },
                        {
                          "choice": "iOS",
                          "description": "iOS mobile"
                        },
                        {
                          "choice": "android",
                          "description": "android mobile"
                        },
                        {
                          "choice": "nix",
                          "description": "Linux distributions"
                        }
                      ],
                      "defaultValue": "MacOS|iOS"
                    },
                    "joinedRename": {
                      "type": "generated",
                      "generator": "join",
                      "replaces": "SupportedPlatforms",
                      "parameters": {
                        "symbols": [
                          {
                            "type": "ref",
                            "value": "Platform"
                          }
                        ],
                        "separator": ", ",
                        "removeEmptyValues": true,
                      }
                    }
                  }
                }
                """;

            string sourceSnippet = """
                // This file is generated for platfrom: SupportedPlatforms
                """;

            string expectedSnippet = """
                // This file is generated for platfrom: MacOS, iOS
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, templateConfig },
                //content
                { "sourcFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = environment.GetTempVirtualizedPath();
            string targetDir = environment.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environment.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromString(templateConfig);
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, object?>() { { "Platform", new MultiValueParameter(new[] { "MacOS", "iOS" }) } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void Test_CoaleseWithInvalidSetup()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                name = "test",
                shortName = "test",
                symbols = new
                {
                    safesourcename = new
                    {
                        type = "generated",
                        generator = "coalesce",
                        parameters = new
                        {
                            sourceVariableName = "safe_namespace",
                            fallbackVariableName = "safe_name"
                        },
                        replaces = "%R1%"
                    },
                }
            };

            string sourceSnippet = """
                %R1%
                """;

            string expectedSnippet = """
                %R1%
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "sourceFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //
            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true);
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/templating/issues/4988")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public async void XMLConditionFailure()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                symbols = new
                {
                    A = new
                    {
                        type = "parameter",
                        dataType = "bool",
                        defaultValue = "false"
                    },
                    B = new
                    {
                        type = "parameter",
                        dataType = "bool",
                        defaultValue = "false"
                    },
                }
            };

            string sourceSnippet = """
                <!--#if (A) -->
                <!-- comment foo -->
                foo
                <!--#endif -->
                <!--#if (B) -->
                This text should not be generated, just to make file content longer to prove the bug.
                If the buffer is advanced when evaluating condition, the bug won't be reproduced. 
                This text ensures that buffer is long enough even considering very-very-long env variable names available on CI machine.
                <!--#endif -->
                """;

            string expectedSnippet = """
                <!-- comment foo -->
                foo
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },

                //content
                { "sourceFile.md", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //
            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new();

            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parameters = new(
                runnableConfig,
                new Dictionary<string, object?>() { { "A", true } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //
            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parameters, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile.md"));
            Assert.Equal(expectedSnippet, resultContent);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/templating/issues/4988")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public async void HashConditionFailure()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                symbols = new
                {
                    A = new
                    {
                        type = "parameter",
                        dataType = "bool",
                        defaultValue = "false"
                    },
                    B = new
                    {
                        type = "parameter",
                        dataType = "bool",
                        defaultValue = "false"
                    },
                }
            };

            string sourceSnippet = """
                #if (A)
                # comment foo
                foo
                #endif
                ##if (B)
                This text should not be generated, just to make file content longer to prove the bug.
                If the buffer is advanced when evaluating condition, the bug won't be reproduced. 
                This text ensures that buffer is long enough even considering very-very-long env variable names available on CI machine.
                #endif
                """;

            string expectedSnippet = """
                # comment foo
                foo
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },

                //content
                { "sourceFile.yaml", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //
            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parameters = new(
                runnableConfig,
                new Dictionary<string, object?>() { { "A", true } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //
            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parameters, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile.yaml"));
            Assert.Equal(expectedSnippet, resultContent);
        }
    }
}
