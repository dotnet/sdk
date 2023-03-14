// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

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

        [Theory]
        [InlineData(ValidChoiceDefinition, false, true)]
        [InlineData(InvalidMultiChoiceDefinition, false, true)]
        [InlineData(ValidChoiceDefinition, true, true)]
        [InlineData(InvalidMultiChoiceDefinition, true, false)]
        public async Task PerformTemplateValidation_ChoiceValuesValidation(string paramDefintion, bool isMultichoice, bool expectedToBeValid)
        {
            //
            // Template content preparation
            //

            Guid inputTestGuid = new("12aa8f4e-a4aa-4ac1-927c-94cb99485ef1");
            string contentFileNamePrefix = "content - ";
            JObject choiceParam = JObject.Parse(paramDefintion);
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
            await templateConfig.ValidateAsync(ValidationScope.Instantiation, default).ConfigureAwait(false);

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
    }
}
