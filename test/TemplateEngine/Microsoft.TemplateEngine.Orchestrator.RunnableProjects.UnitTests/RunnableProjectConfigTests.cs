// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel;
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

        private const string InvalidMultiChoiceDefinition = @"
{
    ""type"": ""parameter"",
    ""description"": ""sample switch"",
    ""datatype"": ""choice"",
    ""allowMultipleValues"": true,
    ""choices"": [
    {
        ""choice"": ""First|Choice"",
        ""description"": ""First Sample Choice""

    },
    {
        ""choice"": ""SecondChoice"",
        ""description"": ""Second Sample Choice""
    },
    {
        ""choice"": ""ThirdChoice"",
        ""description"": ""Third Sample Choice""

    }
    ],
    ""defaultValue "": ""ThirdChoice ""
}
";

        private const string ValidChoiceDefinition = @"
{
    ""type"": ""parameter"",
    ""description"": ""sample switch"",
    ""datatype"": ""choice"",
    ""allowMultipleValues"": true,
    ""choices"": [
    {
        ""choice"": ""FirstChoice"",
        ""description"": ""First Sample Choice""

    },
    {
        ""choice"": ""SecondChoice"",
        ""description"": ""Second Sample Choice""
    },
    {
        ""choice"": ""ThirdChoice"",
        ""description"": ""Third Sample Choice""

    }
    ],
    ""defaultValue "": ""ThirdChoice ""
}
";

        [Theory]
        [InlineData(ValidChoiceDefinition, false, true)]
        [InlineData(InvalidMultiChoiceDefinition, false, true)]
        [InlineData(ValidChoiceDefinition, true, true)]
        [InlineData(InvalidMultiChoiceDefinition, true, false)]
        public void PerformTemplateValidation_ChoiceValuesValidation(string paramDefintion, bool isMultichoice, bool expectedToBeValid)
        {
            //
            // Template content preparation
            //

            Guid inputTestGuid = new Guid("12aa8f4e-a4aa-4ac1-927c-94cb99485ef1");
            string contentFileNamePrefix = "content - ";
            JObject choiceParam = JObject.Parse(paramDefintion);
            choiceParam["AllowMultipleValues"] = isMultichoice;
            TemplateConfigModel config = new TemplateConfigModel()
            {
                Identity = "test",
                Name = "name",
                ShortNameList = new[] { "shortName" },
                Symbols = new[]
                {
                    new ParameterSymbol( "ParamA", choiceParam, null)
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());

            //content
            foreach (string guidFormat in GuidMacroConfig.DefaultFormats.Select(c => c.ToString()))
            {
                templateSourceFiles.Add(contentFileNamePrefix + guidFormat, inputTestGuid.ToString(guidFormat));
            }

            //
            // Dependencies preparation and mounting
            //

            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment(addLoggerProviders: new[] { loggerProvider });
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();

            TestFileSystemHelper.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TestFileSystemHelper.CreateMountPoint(environment, sourceBasePath);
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, config, sourceMountPoint.FileInfo(TestFileSystemHelper.DefaultConfigRelativePath));

            if (expectedToBeValid)
            {
                runnableConfig.PerformTemplateValidation();
                Assert.Empty(loggedMessages.Where(l => l.Item1 >= LogLevel.Warning));
            }
            else
            {
                var exc = Assert.Throws<TemplateValidationException>(runnableConfig.PerformTemplateValidation);
                Assert.Contains("The template configuration ", exc.Message);
                Assert.Contains(" is not valid.", exc.Message);
                Assert.Single(loggedMessages.Where(l => l.Item1 >= LogLevel.Warning));
                string errorMessage = loggedMessages.First(l => l.Item1 >= LogLevel.Warning).Item2;
                Assert.Contains(
                    "Choice parameter  is invalid. It allows multiple values ('AllowMultipleValues=true'), while some of the configured choices contain separator characters ('|', ','). Invalid choices: {First|Choice}",
                    errorMessage);
            }
        }
    }
}
