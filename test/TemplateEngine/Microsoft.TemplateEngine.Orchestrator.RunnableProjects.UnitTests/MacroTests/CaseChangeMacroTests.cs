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
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class CaseChangeMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        public CaseChangeMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }
        [Fact(DisplayName = nameof(TestCaseChangeToLowerConfig))]
        public void TestCaseChangeToLowerConfig()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";
            bool toLower = true;

            CaseChangeMacroConfig macroConfig = new CaseChangeMacroConfig(variableName, null, sourceVariable, toLower);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            string sourceValue = "Original Value SomethingCamelCase";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            CaseChangeMacro macro = new CaseChangeMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter convertedParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out convertedParam));
            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            Assert.Equal(convertedValue, sourceValue.ToLower());
        }

        [Fact(DisplayName = nameof(TestCaseChangeToUpperConfig))]
        public void TestCaseChangeToUpperConfig()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";
            bool toLower = false;

            CaseChangeMacroConfig macroConfig = new CaseChangeMacroConfig(variableName, null, sourceVariable, toLower);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            string sourceValue = "Original Value SomethingCamelCase";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            CaseChangeMacro macro = new CaseChangeMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter convertedParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out convertedParam));
            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            Assert.Equal(convertedValue, sourceValue.ToUpper());
        }

        [Fact(DisplayName = nameof(TestDeferredCaseChangeConfig))]
        public void TestDeferredCaseChangeConfig()
        {
            string variableName = "myString";

            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            string sourceVariable = "sourceString";
            jsonParameters.Add("source", sourceVariable);
            jsonParameters.Add("toLower", false);
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("CaseChangeMacro", null, variableName, jsonParameters);

            CaseChangeMacro macro = new CaseChangeMacro();
            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            string sourceValue = "Original Value SomethingCamelCase";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter convertedParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out convertedParam));
            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            Assert.Equal(convertedValue, sourceValue.ToUpper());
        }
    }
}
