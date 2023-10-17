// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Fakes;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class RunnableProjectConfigTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public RunnableProjectConfigTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        private const string InvalidMultiChoiceDefinition = /*lang=json,strict*/ """
            {
                "type": "parameter",
                "description": "sample switch",
                "datatype": "choice",
                "allowMultipleValues": true,
                "choices": [
                {
                    "choice": "First|Choice",
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
                "defaultValue ": "ThirdChoice "
            }
            """;

        private const string ValidChoiceDefinition = /*lang=json,strict*/ """
            {
                "type": "parameter",
                "description": "sample switch",
                "datatype": "choice",
                "allowMultipleValues": true,
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
                "defaultValue ": "ThirdChoice "
            }
            """;

        private const string ValidComputedDefinition = /*lang=json,strict*/ """
            {
                "type": "computed",
                "datatype": "bool",
                "value": "10 != 11"
            }
            """;

        private const string ValidCustomMacroDefinition = /*lang=json,strict*/ """
            {
                "type": "fake",
                "generator": "fake",
                "parameters": {
                  "source": "dummy",
                  "pattern": "^hello$",
                  "name": "dummy"
                }
            }
            """;

        [Theory]
        [InlineData(ValidChoiceDefinition, false, true)]
        [InlineData(InvalidMultiChoiceDefinition, false, true)]
        [InlineData(ValidChoiceDefinition, true, true)]
        [InlineData(InvalidMultiChoiceDefinition, true, false)]
        public async Task PerformTemplateValidation_ChoiceValuesValidation(string paramDefinition, bool isMultichoice, bool expectedToBeValid)
        {
            //
            // Template content preparation
            //

            Guid inputTestGuid = new("12aa8f4e-a4aa-4ac1-927c-94cb99485ef1");
            string contentFileNamePrefix = "content - ";
            JObject choiceParam = JObject.Parse(paramDefinition);
            choiceParam["AllowMultipleValues"] = isMultichoice;
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "name",
                ShortNameList = new[] { "shortName" },
                Symbols = new[]
                {
                    new ParameterSymbol("ParamA", choiceParam, null)
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

            List<(LogLevel, string)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(addLoggerProviders: new[] { loggerProvider });
            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(environmentSettings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = environmentSettings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new();

            using RunnableProjectConfig templateConfig = new RunnableProjectConfig(environmentSettings, rpg, config, sourceMountPoint.Root);
            await templateConfig.ValidateAsync(ValidationScope.Instantiation, default);

            if (expectedToBeValid)
            {
                Assert.True(templateConfig.IsValid);
                Assert.Empty(templateConfig.ValidationErrors.Where(e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning));
            }
            else
            {
                Assert.False(templateConfig.IsValid);
                IValidationEntry validationError = Assert.Single(templateConfig.ValidationErrors, e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning);
                Assert.Equal("MV004", validationError.Code);
                Assert.Equal(
                    "Choice parameter 'ParamA' is invalid. It allows multiple values ('AllowMultipleValues=true'), while some of the configured choices contain separator characters ('|', ','). Invalid choices: {First|Choice}",
                    validationError.ErrorMessage);
            }
        }

        [Fact]
        public void VerifyComputedSymbolsParsedCorrectly()
        {
            //
            // Template content preparation
            //

            //fill test data here
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "name",
                ShortNameList = new[] { "shortName" },
                Symbols = new[]
                {
                    new ComputedSymbol("computed1", JObject.Parse(ValidComputedDefinition)),
                    new ComputedSymbol("computed2", JObject.Parse(ValidComputedDefinition))
                }
            };

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment();

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            using RunnableProjectConfig templateConfig = new RunnableProjectConfig(environmentSettings, new RunnableProjectGenerator(), config, mountPoint.Root);

            //verify
            Assert.Equal(7, templateConfig.GlobalOperationConfig.Macros.Count);
            var evaluatedMacros = templateConfig.GlobalOperationConfig.Macros.Where(m => m.Type == "evaluate");
            Assert.Equal(2, evaluatedMacros.Count());
            evaluatedMacros.Select(m => m.VariableName).Should().Equal(new string[] { "computed1", "computed2" });
            // default symbols are different for OS
            templateConfig.GlobalOperationConfig.SymbolNames.Count.Should().BeGreaterThanOrEqualTo(3);
            templateConfig.GlobalOperationConfig.SymbolNames.Should().Contain(new string[] { "computed1", "computed2" });
        }

        [Fact]
        public void VerifyCustomGeneratedSymbolsParsedCorrectly()
        {
            //
            // Template content preparation
            //

            //fill test data here
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "name",
                ShortNameList = new[] { "shortName" },
                Symbols = new[]
                {
                    new GeneratedSymbol("fake1", JObject.Parse(ValidCustomMacroDefinition))
                }
            };

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(
                additionalComponents: new[]
                    {
                        (typeof(IMacro), (IIdentifiedComponent)new FakeMacro()),
                        (typeof(IGeneratedSymbolMacro), new FakeMacro())
                    });

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            using RunnableProjectConfig templateConfig = new RunnableProjectConfig(environmentSettings, new RunnableProjectGenerator(), config, mountPoint.Root);

            var customMacros = templateConfig.GlobalOperationConfig.Macros.Where(m => m.Type == "fake");
            //verify
            Assert.Equal(6, templateConfig.GlobalOperationConfig.Macros.Count);
            Assert.Single(customMacros);
            Assert.Equal("fake1", customMacros.First().VariableName);
            // default symbols are different for OS
            templateConfig.GlobalOperationConfig.SymbolNames.Count.Should().BeGreaterThanOrEqualTo(2);
            templateConfig.GlobalOperationConfig.SymbolNames.Should().Contain(new string[] { "fake1" });
        }

        [Fact]
        public void VerifyDerivedSymbolsParsedCorrectly()
        {
            //
            // Template content preparation
            //

            //fill test data here
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "name",
                ShortNameList = new[] { "shortName" },
                Symbols = new[]
                {
                    (BaseSymbol)new ParameterSymbol("original", "whatever"),
                    new DerivedSymbol("derivedTest", valueTransform: "fakeForm", valueSource: "original", replaces: "something")
                }
            };

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(
                additionalComponents: new[]
                    {
                        (typeof(IMacro), (IIdentifiedComponent)new FakeMacro()),
                        (typeof(IGeneratedSymbolMacro), new FakeMacro())
                    });

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            using RunnableProjectConfig templateConfig = new RunnableProjectConfig(environmentSettings, new RunnableProjectGenerator(), config, mountPoint.Root);

            //verify
            Assert.Equal(7, templateConfig.GlobalOperationConfig.Macros.Count);
            Assert.Equal(1, templateConfig.GlobalOperationConfig.Macros.Count(m => m.VariableName == "derivedTest{-VALUE-FORMS-}identity"));

            templateConfig.GlobalOperationConfig.SymbolNames.Should().Contain(new string[] { "original", "derivedTest" });
        }
    }
}
