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
    public class JoinMacroTest : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public JoinMacroTest(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Theory(DisplayName = nameof(TestJoinConstantAndReferenceSymbolConfig))]
        [InlineData(",")]
        [InlineData("")]
        [InlineData(null)]
        public void TestJoinConstantAndReferenceSymbolConfig(string separator)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string constantValue = "constantValue";

            List<KeyValuePair<string, string>> definitions = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("const", constantValue),
                new KeyValuePair<string, string>("ref", referenceSymbolName)
            };

            JoinMacroConfig macroConfig = new JoinMacroConfig(variableName, null, definitions, separator);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new RunnableProjectGenerator.ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            Parameter referenceParam = new Parameter
            {
                IsVariable = true,
                Name = referenceSymbolName
            };

            variables[referenceSymbolName] = referenceSymbolValue;
            setter(referenceParam, referenceSymbolValue);

            JoinMacro macro = new JoinMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig, parameters, setter);

            Assert.True(parameters.TryGetParameterDefinition(variableName, out ITemplateParameter convertedParam));

            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            string expectedValue = string.Join(separator, constantValue, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }

        [Theory(DisplayName = nameof(TestDeferredJoinConfig))]
        [InlineData(",")]
        [InlineData("")]
        [InlineData(null)]
        public void TestDeferredJoinConfig(string separator)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string constantValue = "constantValue";

            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            string symbols =
                $"[ {{\"type\":\"const\" , \"value\":\"{constantValue}\"  }}, {{\"type\":\"ref\" , \"value\":\"{referenceSymbolName}\"  }} ]";
            jsonParameters.Add("symbols", JArray.Parse(symbols));
            if (!string.IsNullOrEmpty(separator))
            {
                jsonParameters.Add("separator", separator);
            }

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("JoinMacro", null, variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new RunnableProjectGenerator.ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            Parameter referenceParam = new Parameter
            {
                IsVariable = true,
                Name = referenceSymbolName
            };

            variables[referenceSymbolName] = referenceSymbolValue;
            setter(referenceParam, referenceSymbolValue);

            JoinMacro macro = new JoinMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig, parameters, setter);

            Assert.True(parameters.TryGetParameterDefinition(variableName, out ITemplateParameter convertedParam));

            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            string expectedValue = string.Join(separator, constantValue, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }
    }
}
