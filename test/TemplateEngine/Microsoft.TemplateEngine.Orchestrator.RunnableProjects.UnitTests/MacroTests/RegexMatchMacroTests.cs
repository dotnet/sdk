// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class RegexMatchMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RegexMatchMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestRegexMatchMacroTrue))]
        public void TestRegexMatchMacroTrue()
        {
            const string variableName = "isMatch";
            const string sourceVariable = "originalValue";
            RegexMatchMacroConfig macroConfig = new RegexMatchMacroConfig(variableName, null, sourceVariable, @"(((?<=\.)|^)(?=\d)|[^\w\.])");

            IVariableCollection variables = new VariableCollection();

            const string sourceValue = "1234test";
            const bool expectedValue = true;
            variables[sourceVariable] = sourceValue;

            RegexMatchMacro macro = new RegexMatchMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            bool newValue = (bool)variables[variableName];
            Assert.Equal(expectedValue, newValue);
        }

        [Fact(DisplayName = nameof(TestRegexMatchMacroFalse))]
        public void TestRegexMatchMacroFalse()
        {
            const string variableName = "isMatch";
            const string sourceVariable = "originalValue";
            RegexMatchMacroConfig macroConfig = new RegexMatchMacroConfig(variableName, null, sourceVariable, @"(((?<=\.)|^)(?=\d)|[^\w\.])");

            IVariableCollection variables = new VariableCollection();

            const string sourceValue = "A1234test";
            const bool expectedValue = false;
            variables[sourceVariable] = sourceValue;

            RegexMatchMacro macro = new RegexMatchMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            bool newValue = (bool)variables[variableName];
            Assert.Equal(expectedValue, newValue);
        }
    }
}
