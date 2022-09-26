// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class RegexMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RegexMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestRegexMacro))]
        public void TestRegexMacro()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            List<KeyValuePair<string?, string?>> steps = new()
            {
                new KeyValuePair<string?, string?>("2+", "3"),
                new KeyValuePair<string?, string?>("13", "Z")
            };
            RegexMacroConfig macroConfig = new RegexMacroConfig(variableName, null, sourceVariable, steps);

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "QQQ121222112";
            string expectedValue = "QQQZZ1Z";

            variables[sourceVariable] = sourceValue;

            RegexMacro macro = new RegexMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            string newValue = (string)variables[variableName];
            Assert.Equal(newValue, expectedValue);
        }

        [Fact(DisplayName = nameof(TestRegexDeferredConfig))]
        public void TestRegexDeferredConfig()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>
            {
                { "source", sourceVariable }
            };

            string jsonSteps = /*lang=json*/ """
                [
                    { 
                        'regex': 'A',
                        'replacement': 'Z'
                    }
                ]
                """;
            jsonParameters.Add("steps", JArray.Parse(jsonSteps));

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("RegexMacro", "string", variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "ABCAABBCC";
            string expectedValue = "ZBCZZBBCC";

            variables[sourceVariable] = sourceValue;

            RegexMacro macro = new RegexMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);
            string newValue = (string)variables[variableName];
            Assert.Equal(newValue, expectedValue);
        }
    }
}
