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
    public class RegexMatchMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RegexMatchMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestRegexMatchMacroTrue))]
        public void TestRegexMatchMacroTrue()
        {
            const string variableName = "isMatch";
            const string sourceVariable = "originalValue";
            RegexMatchMacro macro = new();
            RegexMatchMacroConfig macroConfig = new(macro, variableName, null, sourceVariable, @"(((?<=\.)|^)(?=\d)|[^\w\.])");

            IVariableCollection variables = new VariableCollection();

            const string sourceValue = "1234test";
            const bool expectedValue = true;
            variables[sourceVariable] = sourceValue;

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            bool newValue = (bool)variables[variableName];
            Assert.Equal(expectedValue, newValue);
        }

        [Fact(DisplayName = nameof(TestRegexMatchMacroFalse))]
        public void TestRegexMatchMacroFalse()
        {
            const string variableName = "isMatch";
            const string sourceVariable = "originalValue";

            RegexMatchMacro macro = new();
            RegexMatchMacroConfig macroConfig = new(macro, variableName, null, sourceVariable, @"(((?<=\.)|^)(?=\d)|[^\w\.])");

            IVariableCollection variables = new VariableCollection();

            const string sourceValue = "A1234test";
            const bool expectedValue = false;
            variables[sourceVariable] = sourceValue;

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            bool newValue = (bool)variables[variableName];
            Assert.Equal(expectedValue, newValue);
        }

        [Fact]
        public void GeneratedSymbolTest()
        {
            string variableName = "isMatch";
            string sourceVariable = "originalValue";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) },
                { "pattern", JExtensions.ToJsonString(@"(((?<=\.)|^)(?=\d)|[^\w\.])") }
            };
            GeneratedSymbol symbol = new(variableName, "regexMatch", jsonParameters, "string");

            IVariableCollection variables = new VariableCollection();

            const string sourceValue = "1234test";
            const bool expectedValue = true;
            variables[sourceVariable] = sourceValue;

            RegexMatchMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            bool newValue = (bool)variables[variableName];
            Assert.Equal(expectedValue, newValue);
        }

        [Fact]
        public void MissingSourceVariableTest()
        {
            string variableName = "isMatch";
            string sourceVariable = "originalValue";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) },
                { "pattern", JExtensions.ToJsonString(@"(((?<=\.)|^)(?=\d)|[^\w\.])") }
            };
            GeneratedSymbol symbol = new(variableName, "regexMatch", jsonParameters, "string");

            IVariableCollection variables = new VariableCollection();

            RegexMatchMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            Assert.False(variables.ContainsKey(variableName));
        }

        [Fact]
        public void InvalidConfigurationTest_MissingSource()
        {
            RegexMatchMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "pattern", JExtensions.ToJsonString(@"(((?<=\.)|^)(?=\d)|[^\w\.])") }
            };

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "regexMatch", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'regexMatch' should have 'source' property defined.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_MissingPattern()
        {
            RegexMatchMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString("src") },
            };

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "regexMatch", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'regexMatch' should have 'pattern' property defined.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_InvalidPattern()
        {
            RegexMatchMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString("src") },
                { "pattern", JExtensions.ToJsonString(@"(()") }
            };

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "regexMatch", jsonParameters)));
            Assert.Equal("Generated symbol 'test': the regex pattern '(()' is invalid.", ex.Message);
        }

    }
}
