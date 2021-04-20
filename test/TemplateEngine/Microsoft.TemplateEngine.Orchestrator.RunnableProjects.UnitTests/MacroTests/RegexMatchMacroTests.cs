// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class RegexMatchMacroTests : TestBase
    {
        [Fact(DisplayName = nameof(TestRegexMatchMacroTrue))]
        public void TestRegexMatchMacroTrue()
        {
            const string variableName = "isMatch";
            const string sourceVariable = "originalValue";
            RegexMatchMacroConfig macroConfig = new RegexMatchMacroConfig(variableName, null, sourceVariable, @"(((?<=\.)|^)(?=\d)|[^\w\.])");

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new RunnableProjectGenerator.ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            const string sourceValue = "1234test";
            const bool expectedValue = true;

            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            RegexMatchMacro macro = new RegexMatchMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);

            Assert.True(parameters.TryGetParameterDefinition(variableName, out ITemplateParameter newParam));
            bool newValue = (bool)parameters.ResolvedValues[newParam];
            Assert.Equal(newValue, expectedValue);
        }

        [Fact(DisplayName = nameof(TestRegexMatchMacroFalse))]
        public void TestRegexMatchMacroFalse()
        {
            const string variableName = "isMatch";
            const string sourceVariable = "originalValue";
            RegexMatchMacroConfig macroConfig = new RegexMatchMacroConfig(variableName, null, sourceVariable, @"(((?<=\.)|^)(?=\d)|[^\w\.])");

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new RunnableProjectGenerator.ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            const string sourceValue = "A1234test";
            const bool expectedValue = false;

            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            RegexMatchMacro macro = new RegexMatchMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);

            Assert.True(parameters.TryGetParameterDefinition(variableName, out ITemplateParameter newParam));
            bool newValue = (bool)parameters.ResolvedValues[newParam];
            Assert.Equal(newValue, expectedValue);
        }
    }
}
