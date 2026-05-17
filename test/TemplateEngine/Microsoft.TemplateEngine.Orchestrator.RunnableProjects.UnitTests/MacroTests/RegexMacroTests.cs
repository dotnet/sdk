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
    public class RegexMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RegexMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestRegexMacro))]
        public void TestRegexMacro()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            List<(string, string)> steps = new()
            {
                ("2+", "3"),
                ("13", "Z")
            };

            RegexMacro macro = new();
            RegexMacroConfig macroConfig = new(macro, variableName, null, sourceVariable, steps);

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "QQQ121222112";
            string expectedValue = "QQQZZ1Z";

            variables[sourceVariable] = sourceValue;

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            string newValue = (string)variables[variableName];
            Assert.Equal(newValue, expectedValue);
        }

        [Fact]
        public void GeneratedSymbolTest()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) }
            };

            string jsonSteps = /*lang=json*/ """
                [
                    {
                        "regex": "A",
                        "replacement": "Z"
                    }
                ]
                """;
            jsonParameters.Add("steps", jsonSteps);

            GeneratedSymbol symbol = new(variableName, "regex", jsonParameters, "string");

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "ABCAABBCC";
            string expectedValue = "ZBCZZBBCC";

            variables[sourceVariable] = sourceValue;

            RegexMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            string newValue = (string)variables[variableName];
            Assert.Equal(newValue, expectedValue);
        }

        [Fact]
        public void MissingSourceVariableTest()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) }
            };

            string jsonSteps = /*lang=json*/ """
                [
                    {
                        "regex": "A",
                        "replacement": "Z"
                    }
                ]
                """;
            jsonParameters.Add("steps", jsonSteps);

            GeneratedSymbol symbol = new(variableName, "regex", jsonParameters, "string");
            IVariableCollection variables = new VariableCollection();

            RegexMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            Assert.False(variables.ContainsKey(variableName));
        }

        [Fact]
        public void InvalidConfigurationTest_MissingSource()
        {
            RegexMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            string jsonSteps = /*lang=json*/ """
                [
                    {
                        "regex": "A",
                        "replacement": "Z"
                    }
                ]
                """;
            jsonParameters.Add("steps", jsonSteps);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "regex", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'regex' should have 'source' property defined.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingSteps()
        {
            RegexMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString("src") }
            };

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "regex", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'regex' should have 'steps' property defined.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingRegex()
        {
            RegexMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString("src") }
            };
            string jsonSteps = /*lang=json*/ """
                [
                    {
                        "replacement": "Z"
                    }
                ]
                """;
            jsonParameters.Add("steps", jsonSteps);
            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "regex", jsonParameters)));
            Assert.Equal("Generated symbol 'test': array 'steps' should contain JSON objects with property 'regex'.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingReplacement()
        {
            RegexMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString("src") }
            };
            string jsonSteps = /*lang=json*/ """
                [
                    {
                        "regex": "A"
                    }
                ]
                """;
            jsonParameters.Add("steps", jsonSteps);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "regex", jsonParameters)));
            Assert.Equal("Generated symbol 'test': array 'steps' should contain JSON objects with property 'replacement'.", ex.Message);
        }
    }
}
