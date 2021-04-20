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
    public class ConstantMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        public ConstantMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestConstantConfig))]
        public void TestConstantConfig()
        {
            string variableName = "myConstant";
            string value = "1048576";
            ConstantMacroConfig macroConfig = new ConstantMacroConfig(null, variableName, value);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            ConstantMacro macro = new ConstantMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter constParameter;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out constParameter));
            string constParamValue = (parameters.ResolvedValues[constParameter]).ToString();
            Assert.Equal(constParamValue, value);
        }

        [Fact(DisplayName = nameof(TestConstantDeferredConfig))]
        public void TestConstantDeferredConfig()
        {
            string variableName = "myConstant";
            string value = "1048576";
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            jsonParameters.Add("value", value);
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("ConstantMacro", null, variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            ConstantMacro macro = new ConstantMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter constParameter;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out constParameter));
            string constParamValue = (parameters.ResolvedValues[constParameter]).ToString();
            Assert.Equal(constParamValue, value);
        }
    }
}
