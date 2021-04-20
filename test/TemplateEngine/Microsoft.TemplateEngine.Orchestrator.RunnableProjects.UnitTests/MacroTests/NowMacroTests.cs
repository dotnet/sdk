// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;
using Newtonsoft.Json.Linq;
using Xunit;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class NowMacroTests : TestBase
    {
        // tests regular Now configuration, and Utc = true
        [Fact(DisplayName = nameof(EvaluateNowConfig))]
        public void EvaluateNowConfig()
        {
            string variableName = "nowString";
            string format = "";
            bool utc = true;
            NowMacroConfig macroConfig = new NowMacroConfig(variableName, null, format, utc);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            NowMacro macro = new NowMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);
            ITemplateParameter resultParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out resultParam));
            string macroNowString = (string)parameters.ResolvedValues[resultParam];
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.UtcNow);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.True(difference.TotalSeconds < 10);
        }

        // tests NowMacro deferred configuration, and Utc = false
        [Fact(DisplayName = nameof(EvaluateNowDeferredConfig))]
        public void EvaluateNowDeferredConfig()
        {
            string variableName = "nowString";
            string format = "";
            bool utc = false;
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            jsonParameters.Add("format", format);
            jsonParameters.Add("utc", utc);
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("NowMacro", null, variableName, jsonParameters);


            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            NowMacro macro = new NowMacro();
            IMacroConfig realConfig = macro.CreateConfig(EngineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter resultParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out resultParam));
            string macroNowString = (string)parameters.ResolvedValues[resultParam];
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.Now);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.True(difference.TotalSeconds < 10);
        }
    }
}
