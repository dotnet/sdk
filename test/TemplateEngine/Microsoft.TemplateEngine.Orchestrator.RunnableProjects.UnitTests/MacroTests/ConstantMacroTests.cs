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
    public class ConstantMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public ConstantMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestConstantConfig))]
        public void TestConstantConfig()
        {
            string variableName = "myConstant";
            string value = "1048576";
            ConstantMacro macro = new();
            ConstantMacroConfig macroConfig = new(macro, null, variableName, value);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            Assert.Equal(value, variables[variableName]);
        }

        [Fact]
        public void GeneratedSymbolTest()
        {
            string variableName = "myConstant";
            string value = "1048576";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "value", JExtensions.ToJsonString(value) }
            };

            ConstantMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            Assert.Equal(value, variables[variableName]);
        }

        [Fact]
        public void GeneratedSymbolTest_Bool()
        {
            string variableName = "myConstant";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "value", JExtensions.ToJsonString(true) }
            };

            ConstantMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            Assert.Equal("True", variables[variableName]);
        }

        [Fact]
        public void GeneratedSymbolTest_Int()
        {
            string variableName = "myConstant";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "value", JExtensions.ToJsonString(1000) }
            };

            ConstantMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            Assert.Equal("1000", variables[variableName]);
        }

        [Fact]
        [Obsolete("EvaluateConfig is deprecated")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "myConstant";
            string value = "1048576";
            ConstantMacro macro = new();
            ConstantMacroConfig macroConfig = new(macro, null, variableName, value);

            IVariableCollection variables = new VariableCollection();

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            Assert.Equal(value, variables[variableName]);
        }

        [Fact]
        public void InvalidConfigurationTest()
        {
            ConstantMacro macro = new();
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "constant", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'constant' should have 'value' property defined.", ex.Message);
        }
    }
}
