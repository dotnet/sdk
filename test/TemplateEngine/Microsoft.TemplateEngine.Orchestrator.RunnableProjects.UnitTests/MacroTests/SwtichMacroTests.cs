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
    public class SwtichMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public SwtichMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestSwitchConfig))]
        public void TestSwitchConfig()
        {
            string variableName = "mySwitchVar";
            string evaluator = "C++";
            string dataType = "string";
            string expectedValue = "this one";
            List<(string?, string)> switches = new()
            {
                ("(3 > 10)", "three greater than ten - false"),
                ("(false)", "false value"),
                ("(10 > 0)", expectedValue),
                ("(5 > 4)", "not this one")
            };
            SwitchMacro macro = new();
            SwitchMacroConfig macroConfig = new(macro, variableName, evaluator, dataType, switches);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            string resultValue = (string)variables[variableName];
            Assert.Equal(resultValue, expectedValue);
        }

        [Fact]
        public void GeneratedSymbolTest()
        {
            string variableName = "mySwitchVar";
            string evaluator = "C++";
            string dataType = "string";
            string expectedValue = "this one";
            string switchCases = @"[
                {
                    'condition': '(3 > 10)',
                    'value': 'three greater than ten'
                },
                {
                    'condition': '(false)',
                    'value': 'false value'
                },
                {
                    'condition': '(10 > 0)',
                    'value': '" + expectedValue + @"'
                },
                {
                    'condition': '(5 > 4)',
                    'value': 'not this one'
                }
            ]";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "evaluator", JExtensions.ToJsonString(evaluator) },
                { "datatype",  JExtensions.ToJsonString(dataType) },
                { "cases", switchCases }
            };

            GeneratedSymbol symbol = new(variableName, "switch", jsonParameters, dataType);

            IVariableCollection variables = new VariableCollection();

            SwitchMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            string resultValue = (string)variables[variableName];
            Assert.Equal(resultValue, expectedValue);
        }

        [Theory]
        [InlineData("A", "condition")]
        [InlineData("B", "default")]
        [InlineData(null, "default")]
        public void DependantConditionTest(string? varValue, string expectedResult)
        {
            string variableName = "mySwitchVar";
            string evaluator = "C++";
            string dataType = "string";
            List<(string?, string)> switches = new()
            {
                ("(testVar == \"A\")", "condition"),
                (null, "default")
            };
            SwitchMacro macro = new();
            SwitchMacroConfig macroConfig = new(macro, variableName, evaluator, dataType, switches);

            VariableCollection variables = new();
            if (varValue is not null)
            {
                variables["testVar"] = varValue;
            }

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            string resultValue = (string)variables[variableName];
            Assert.Equal(expectedResult, resultValue);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingCases()
        {
            SwitchMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "switch", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'switch' should have 'cases' property defined.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingSymbolValue()
        {
            SwitchMacro macro = new();

            string switchCases = /*lang=json*/ @"[
                {
                    'condition': '(3 > 10)',
                },
                {
                    'value': 'default'
                }
            ]";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "cases", switchCases }
            };

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "switch", jsonParameters)));
            Assert.Equal("Generated symbol 'test': array 'cases' should contain JSON objects with property 'value'.", ex.Message);
        }

        [Fact]
        public void DefaultConfigurationTest()
        {
            SwitchMacro macro = new();

            string switchCases = /*lang=json*/ @"[
                {
                    'condition': '(3 > 10)',
                    'value': 'v'
                },
                {
                    'value': 'default'
                }
            ]";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "cases", switchCases }
            };

            SwitchMacroConfig config = new(macro, new GeneratedSymbol("test", "switch", jsonParameters));

            Assert.Equal(EvaluatorSelector.SelectStringEvaluator(EvaluatorType.CPP2), config.Evaluator);
        }
    }
}
