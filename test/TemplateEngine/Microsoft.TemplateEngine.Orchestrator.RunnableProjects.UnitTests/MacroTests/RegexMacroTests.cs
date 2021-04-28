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
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class RegexMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RegexMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestRegexMacro))]
        public void TestRegexMacro()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            IList<KeyValuePair<string, string>> steps = new List<KeyValuePair<string, string>>();
            steps.Add(new KeyValuePair<string, string>("2+", "3"));
            steps.Add(new KeyValuePair<string, string>("13", "Z"));
            RegexMacroConfig macroConfig = new RegexMacroConfig(variableName, null, sourceVariable, steps);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            string sourceValue = "QQQ121222112";
            string expectedValue = "QQQZZ1Z";

            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            RegexMacro macro = new RegexMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter newParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out newParam));
            string newValue = (string)parameters.ResolvedValues[newParam];
            Assert.Equal(newValue, expectedValue);
        }

        [Fact(DisplayName = nameof(TestRegexDeferredConfig))]
        public void TestRegexDeferredConfig()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            jsonParameters.Add("source", sourceVariable);

            string jsonSteps = @"[
                { 
                    'regex': 'A',
                    'replacement': 'Z'
                }
            ]";
            jsonParameters.Add("steps", JArray.Parse(jsonSteps));

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("RegexMacro", "string", variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            string sourceValue = "ABCAABBCC";
            string expectedValue = "ZBCZZBBCC";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            RegexMacro macro = new RegexMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter newParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out newParam));
            string newValue = (string)parameters.ResolvedValues[newParam];
            Assert.Equal(newValue, expectedValue);
        }
    }
}
