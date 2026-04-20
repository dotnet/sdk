// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class SplitConfigurationTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public SplitConfigurationTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        private static string TemplateJsonWithProperAdditionalConfigFilesString
        {
            get
            {
                string templateJsonString = /*lang=json,strict*/ """
                {
                  "author": "Microsoft",
                  "classifications": ["Common", "Console"],
                  "name": "Test Split Config Console App",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "Testing.Split.Config.Console",
                  "identity": "Testing.Framework.Versioned.Console.CSharp",
                  "shortName": "splitconfigtest",
                  "sourceName": "Company.ConsoleApplication1",
                  "preferNameDirectory": true,
                  "additionalConfigFiles": [
                    "symbols.template.json"
                  ],
                  "symbols": {
                    "type": {
                      "type": "parameter",
                      "datatype": "choice",
                      "choices": [
                        {
                          "choice": "project"
                        }
                      ]
                    },
                    "language": {
                      "type": "parameter",
                      "datatype": "choice",
                      "choices": [
                        {
                          "choice": "C#"
                        }
                      ]
                    }
                  }
                }
                """;
                return templateJsonString;
            }
        }

        private static string SymbolsTemplateJsonString
        {
            get
            {
                string symbolsTemplateJsonString = /*lang=json,strict*/ """
                {
                  "symbols": {
                    "RuntimeFrameworkVersion": {
                      "type": "parameter",
                      "replaces": "2.0.0-beta-xyz"
                    },
                    "Framework": {
                                "type": "parameter",
                      "datatype": "choice",
                      "choices": [
                        {
                          "choice": "1.0",
                          "description": "Target netcoreapp1.0"
                        },
                        {
                          "choice": "1.1",
                          "description": "Target netcoreapp1.1"
                        },
                        {
                          "choice": "2.0",
                          "description": "Target netcoreapp2.0 build specified by RuntimeFrameworkVersion"
                        }
                      ],
                      "defaultValue": "1.0"
                    },
                    "MyThing": {
                      "type": "parameter",
                      "datatype": "choice",
                      "choices": [
                        {
                          "choice": "foo"
                        },
                        {
                          "choice": "bar"
                        },
                        {
                          "choice": "baz"
                        }
                      ],
                      "defaultValue": "foo"
                    }
                  }
                }
                """;
                return symbolsTemplateJsonString;
            }
        }

        private static string TemplateJsonWithAdditionalFileOutsideBasePath
        {
            get
            {
                string templateJsonString = /*lang=json,strict*/ """
                {
                  "additionalConfigFiles": [
                    "../../improper.template.json"
                  ]
                }
                """;
                return templateJsonString;
            }
        }

        [Fact(DisplayName = nameof(SplitConfigCantReferenceFileOutsideBasePath))]
        public void SplitConfigCantReferenceFileOutsideBasePath()
        {
            string sourcePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { ".template.config/template.json", TemplateJsonWithAdditionalFileOutsideBasePath }
            };

            _engineEnvironmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            IGenerator generator = new RunnableProjectGenerator();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourcePath);

            IFile? templateConfigFileInfo = mountPoint.FileInfo(".template.config/template.json");
            Assert.NotNull(templateConfigFileInfo);

            Exception e = Assert.Throws<TemplateAuthoringException>(() => new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFileInfo));
            Assert.Equal("Failed to load additional configuration file ../../improper.template.json, the file does not exist.", e.Message);
        }

        [Fact(DisplayName = nameof(SplitConfigReadFailsIfAReferencedFileIsMissing))]
        public void SplitConfigReadFailsIfAReferencedFileIsMissing()
        {
            string sourcePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { ".template.config/template.json", TemplateJsonWithProperAdditionalConfigFilesString }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            IGenerator generator = new RunnableProjectGenerator();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourcePath);

            IFile? templateConfigFileInfo = mountPoint.FileInfo(".template.config/template.json");
            Assert.NotNull(templateConfigFileInfo);

            Exception e = Assert.Throws<TemplateAuthoringException>(() => new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFileInfo));
            Assert.Equal("Failed to load additional configuration file symbols.template.json, the file does not exist.", e.Message);
        }

        [Fact]
        public async Task SplitConfigTest()
        {
            string sourcePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { "templateSource/.template.config/template.json", TemplateJsonWithProperAdditionalConfigFilesString },
                { "templateSource/.template.config/symbols.template.json", SymbolsTemplateJsonString }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            IGenerator generator = new RunnableProjectGenerator();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourcePath);

            IFile? templateConfigFileInfo = mountPoint.FileInfo("templateSource/.template.config/template.json");
            Assert.NotNull(templateConfigFileInfo);

            ScannedTemplateInfo config = new ScannedTemplateInfo(_engineEnvironmentSettings, generator, templateConfigFileInfo);
            ITemplate? template = await generator.LoadTemplateAsync(_engineEnvironmentSettings, config, baselineName: null, cancellationToken: default);

            Assert.NotNull(template);

            IDictionary<string, ITemplateParameter> parameters = template!.ParameterDefinitions.ToDictionary(p => p.Name, p => p);
            Assert.Equal(6, parameters.Count);  // 5 in the configs + 1 for 'name' (implicit)
            Assert.True(parameters.ContainsKey("type"));
            Assert.True(parameters.ContainsKey("language"));
            Assert.True(parameters.ContainsKey("RuntimeFrameworkVersion"));
            Assert.True(parameters.ContainsKey("Framework"));
            Assert.True(parameters.ContainsKey("MyThing"));
        }
    }
}
