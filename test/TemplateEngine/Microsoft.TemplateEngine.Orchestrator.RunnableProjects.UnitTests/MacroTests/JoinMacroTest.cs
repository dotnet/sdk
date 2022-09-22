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
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public JoinMacroTest(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Theory(DisplayName = nameof(TestJoinConstantAndReferenceSymbolConfig))]
        [InlineData(",", true)]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData(",", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TestJoinConstantAndReferenceSymbolConfig(string separator, bool removeEmptyValues)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string referenceEmptySymbolName = "referenceEmptySymbol";
            string constantValue = "constantValue";

            List<KeyValuePair<string?, string?>> definitions = new()
            {
                new KeyValuePair<string?, string?>("const", constantValue),
                new KeyValuePair<string?, string?>("ref", referenceEmptySymbolName),
                new KeyValuePair<string?, string?>("ref", referenceSymbolName)
            };

            JoinMacroConfig macroConfig = new JoinMacroConfig(variableName, null, definitions, separator, removeEmptyValues);

            IVariableCollection variables = new VariableCollection
            {
                [referenceSymbolName] = referenceSymbolValue
            };

            JoinMacro macro = new JoinMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            string convertedValue = (string)variables[variableName];
            string expectedValue =
                removeEmptyValues ?
                string.Join(separator, constantValue, referenceSymbolValue) :
                string.Join(separator, constantValue, null, referenceSymbolValue);
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

            IVariableCollection variables = new VariableCollection
            {
                [referenceSymbolName] = referenceSymbolValue
            };

            JoinMacro macro = new JoinMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);

            string convertedValue = (string)variables[variableName];
            string expectedValue = string.Join(separator, constantValue, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }
    }
}
