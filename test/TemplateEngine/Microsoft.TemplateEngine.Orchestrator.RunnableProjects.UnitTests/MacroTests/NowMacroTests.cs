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

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class NowMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public NowMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        // tests regular Now configuration, and Utc = true
        [Fact(DisplayName = nameof(EvaluateNowConfig))]
        public void EvaluateNowConfig()
        {
            string variableName = "nowString";
            string format = string.Empty;
            bool utc = true;
            NowMacroConfig macroConfig = new NowMacroConfig(variableName, format, utc);
            Assert.Equal("string", macroConfig.DataType);

            IVariableCollection variables = new VariableCollection();

            NowMacro macro = new NowMacro();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            Assert.IsType<string>(variables[variableName]);
            string macroNowString = (string)variables[variableName]!;
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
            string format = string.Empty;
            bool utc = false;
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>
            {
                { "format", format },
                { "utc", utc }
            };
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("NowMacro", null, variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            NowMacro macro = new NowMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            Assert.Equal("string", (realConfig as NowMacroConfig)?.DataType);

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);
            Assert.IsType<string>(variables[variableName]);
            string macroNowString = (string)variables[variableName]!;
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.Now);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.True(difference.TotalSeconds < 10);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("string")]
        [InlineData("date")]
        public void EvaluateNowOverrideDatatypeInConfig(string type)
        {
            string variableName = "nowString";
            string format = string.Empty;
            bool utc = false;
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>
            {
                { "format", format },
                { "utc", utc },
                { "datatype", type }
            };
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("NowMacro", type, variableName, jsonParameters);
            NowMacro macro = new NowMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            Assert.Equal("string", (realConfig as NowMacroConfig)?.DataType);
        }
    }
}
