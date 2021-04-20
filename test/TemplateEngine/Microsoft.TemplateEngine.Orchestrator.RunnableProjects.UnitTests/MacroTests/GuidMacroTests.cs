// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
    public class GuidMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        public GuidMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }
        [Fact(DisplayName = nameof(TestGuidConfig))]
        public void TestGuidConfig()
        {
            string paramName = "TestGuid";
            IMacroConfig macroConfig = new GuidMacroConfig(paramName, "string", string.Empty, null);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            GuidMacro guidMacro = new GuidMacro();
            guidMacro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig, parameters, setter);
            ValidateGuidMacroCreatedParametersWithResolvedValues(paramName, parameters);
        }

        [Fact(DisplayName = nameof(TestDeferredGuidConfig))]
        public void TestDeferredGuidConfig()
        {
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            jsonParameters.Add("format", null);
            string variableName = "myGuid1";
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("GuidMacro", "string", variableName, jsonParameters);

            GuidMacro guidMacro = new GuidMacro();
            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(_engineEnvironmentSettings, parameters);

            IMacroConfig realConfig = guidMacro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            guidMacro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig, parameters, setter);
            ValidateGuidMacroCreatedParametersWithResolvedValues(variableName, parameters);
        }

        private static void ValidateGuidMacroCreatedParametersWithResolvedValues(string variableName, IParameterSet parameters)
        {
            ITemplateParameter setParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out setParam));

            Guid paramValue = Guid.Parse((string)parameters.ResolvedValues[setParam]);

            // check that all the param name variants were created, and their values all resolve to the same guid.
            string guidFormats = GuidMacroConfig.DefaultFormats;
            for (int i = 0; i < guidFormats.Length; ++i)
            {
                string otherFormatParamName = variableName + "-" + guidFormats[i];
                ITemplateParameter testParam;
                Assert.True(parameters.TryGetParameterDefinition(otherFormatParamName, out testParam));
                Guid testValue = Guid.Parse((string)parameters.ResolvedValues[testParam]);
                Assert.Equal(paramValue, testValue);
            }
        }
    }
}
