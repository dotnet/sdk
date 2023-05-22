// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class JoinMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public JoinMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
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

            List<(JoinMacroConfig.JoinType, string)> definitions = new()
            {
                (JoinMacroConfig.JoinType.Const, constantValue),
                (JoinMacroConfig.JoinType.Ref, referenceEmptySymbolName),
                (JoinMacroConfig.JoinType.Ref, referenceSymbolName)
            };

            JoinMacro macro = new();
            JoinMacroConfig macroConfig = new(macro, variableName, null, definitions, separator, removeEmptyValues);

            IVariableCollection variables = new VariableCollection
            {
                [referenceSymbolName] = referenceSymbolValue
            };

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);
            string convertedValue = (string)variables[variableName];
            string expectedValue =
                removeEmptyValues ?
                string.Join(separator, constantValue, referenceSymbolValue) :
                string.Join(separator, constantValue, null, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }

        [Theory]
        [InlineData(",")]
        [InlineData("")]
        public void GeneratedSymbolTest(string separator)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string constantValue = "constantValue";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            string symbols =
                $"[ {{\"type\":\"const\" , \"value\":\"{constantValue}\"  }}, {{\"type\":\"ref\" , \"value\":\"{referenceSymbolName}\"  }} ]";
            jsonParameters.Add("symbols", symbols);
            if (separator != null)
            {
                jsonParameters.Add("separator", JExtensions.ToJsonString(separator));
            }
            GeneratedSymbol symbol = new(variableName, "JoinMacro", jsonParameters);

            IVariableCollection variables = new VariableCollection
            {
                [referenceSymbolName] = referenceSymbolValue
            };

            JoinMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            string convertedValue = (string)variables[variableName];
            string expectedValue = string.Join(separator, constantValue, referenceSymbolValue);
            Assert.Equal(expectedValue, convertedValue);
        }

        [Fact]
        [Obsolete("IMacro.EvaluateConfig is obsolete")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string referenceEmptySymbolName = "referenceEmptySymbol";
            string constantValue = "constantValue";

            List<(JoinMacroConfig.JoinType, string)> definitions = new()
            {
                (JoinMacroConfig.JoinType.Const, constantValue),
                (JoinMacroConfig.JoinType.Ref, referenceEmptySymbolName),
                (JoinMacroConfig.JoinType.Ref, referenceSymbolName)
            };

            JoinMacro macro = new();
            JoinMacroConfig macroConfig = new(macro, variableName, null, definitions, ",", removeEmptyValues: true);

            IVariableCollection variables = new VariableCollection
            {
                [referenceSymbolName] = referenceSymbolValue
            };

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            string convertedValue = (string)variables[variableName];
            string expectedValue = string.Join(",", constantValue, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingSymbols()
        {
            JoinMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "separator", JExtensions.ToJsonString(",") }
            };

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "join", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'join' should have 'symbols' property defined.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingSymbolValue()
        {
            JoinMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "separator", JExtensions.ToJsonString(",") }
            };
            string symbols = $"[ {{\"type\":\"const\" }}, {{\"type\":\"ref\" , \"value\":\"ref\"  }} ]";
            jsonParameters.Add("symbols", symbols);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "join", jsonParameters)));
            Assert.Equal("Generated symbol 'test': array 'symbols' should contain JSON objects with property 'value'.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_EmptySymbolValue()
        {
            JoinMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "separator", JExtensions.ToJsonString(",") }
            };
            string symbols = $"[ {{\"type\":\"ref\" , \"value\":\"\"  }} ]";
            jsonParameters.Add("symbols", symbols);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "join", jsonParameters)));
            Assert.Equal("Generated symbol 'test': array 'symbols' should contain JSON objects with property non-empty 'value' when 'type' is 'Ref'.", ex.Message);
        }

        [Fact]
        public void DefaultConfigurationTest()
        {
            JoinMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            string symbols = $"[ {{\"value\":\"rep\"  }} ]";
            jsonParameters.Add("symbols", symbols);

            JoinMacroConfig config = new(macro, new GeneratedSymbol("test", "join", jsonParameters));

            Assert.Equal(string.Empty, config.Separator);
            Assert.False(config.RemoveEmptyValues);
            Assert.Equal(JoinMacroConfig.JoinType.Const, config.Symbols.Single().Type);
        }
    }
}
