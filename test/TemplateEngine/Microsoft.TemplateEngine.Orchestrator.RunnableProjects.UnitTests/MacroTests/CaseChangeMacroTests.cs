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
    public class CaseChangeMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public CaseChangeMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestCaseChangeToLowerConfig))]
        public void TestCaseChangeToLowerConfig()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";
            bool toLower = true;

            CaseChangeMacro macro = new();
            CaseChangeMacroConfig macroConfig = new(macro, variableName, null, sourceVariable, toLower);

            IVariableCollection variables = new VariableCollection();
            string sourceValue = "Original Value SomethingCamelCase";
            variables[sourceVariable] = sourceValue;

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            string convertedValue = (string)variables[variableName];
            Assert.Equal(convertedValue, sourceValue.ToLower());
        }

        [Fact(DisplayName = nameof(TestCaseChangeToUpperConfig))]
        public void TestCaseChangeToUpperConfig()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";
            bool toLower = false;

            CaseChangeMacro macro = new();
            CaseChangeMacroConfig macroConfig = new(macro, variableName, null, sourceVariable, toLower);

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "Original Value SomethingCamelCase";
            variables[sourceVariable] = sourceValue;

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            string convertedValue = (string)variables[variableName];
            Assert.Equal(convertedValue, sourceValue.ToUpper());
        }

        [Fact]
        public void GeneratedSymbolTest()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) },
                { "toLower", JExtensions.ToJsonString(false) }
            };
            CaseChangeMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "Original Value SomethingCamelCase";
            variables[sourceVariable] = sourceValue;
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            string convertedValue = (string)variables[variableName];
            Assert.Equal(convertedValue, sourceValue.ToUpper());
        }

        [Fact]
        public void MissingSourceSymbolTest()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) },
                { "toLower", JExtensions.ToJsonString(false) }
            };
            CaseChangeMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            VariableCollection variables = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            Assert.False(variables.ContainsKey(variableName));
        }

        [Fact]
        [Obsolete("IMacro.EvaluateConfig is deprecated")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";
            bool toLower = true;

            CaseChangeMacro macro = new();
            CaseChangeMacroConfig macroConfig = new(macro, variableName, null, sourceVariable, toLower);

            IVariableCollection variables = new VariableCollection();
            string sourceValue = "Original Value SomethingCamelCase";
            variables[sourceVariable] = sourceValue;

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            string convertedValue = (string)variables[variableName];
            Assert.Equal(convertedValue, sourceValue.ToLower());
        }

        [Fact]
        public void InvalidConfigurationTest()
        {
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            CaseChangeMacro macro = new();
            GeneratedSymbol symbol = new("test", macro.Type, jsonParameters);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, symbol));
            Assert.Equal("Generated symbol 'test' of type 'casing' should have 'source' property defined.", ex.Message);
        }

        [Fact]
        public void DefaultConfigurationTest()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) },
            };
            CaseChangeMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            VariableCollection variables = new();
            string sourceValue = "AbC";
            variables[sourceVariable] = sourceValue;
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            //default configuration is lower-case
            Assert.Equal("abc", variables[variableName]);
        }
    }
}
