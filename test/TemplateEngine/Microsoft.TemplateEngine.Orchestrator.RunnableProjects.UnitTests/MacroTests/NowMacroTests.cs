// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class NowMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public NowMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        // tests regular Now configuration, and Utc = true
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("yyyy-MM-dd HH:mm:ss")]
        public void EvaluateNowConfig(string? format)
        {
            string variableName = "nowString";
            bool utc = true;

            NowMacro macro = new();
            NowMacroConfig macroConfig = new(macro, variableName, format, utc);
            Assert.Equal("string", macroConfig.DataType);

            IVariableCollection variables = new VariableCollection();

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
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "format", JExtensions.ToJsonString(format) },
                { "utc", JExtensions.ToJsonString(utc) }
            };
            GeneratedSymbol deferredConfig = new(variableName, "NowMacro", jsonParameters);
            IVariableCollection variables = new VariableCollection();

            NowMacro macro = new();
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
        [InlineData(null, "string")]
        [InlineData("", "string")]
        [InlineData("string", "string")]
        [InlineData("date", "date")]
        public void EvaluateNowOverrideDatatypeInConfig(string type, string expectedType)
        {
            string variableName = "nowString";
            string format = string.Empty;
            bool utc = false;
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "format", JExtensions.ToJsonString(format) },
                { "utc", JExtensions.ToJsonString(utc) }
            };
            GeneratedSymbol deferredConfig = new(variableName, "NowMacro", jsonParameters, type);
            NowMacro macro = new();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            Assert.Equal(expectedType, (realConfig as NowMacroConfig)?.DataType);
        }

        [Fact]
        public void TestDeterministicMode()
        {
            IVariableCollection variables = new VariableCollection();
            NowMacro macro = new();
            NowMacroConfig config = new(macro, "test", "yyyy-MM-dd HH:mm:ss");
            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, config);

            Assert.Equal(1, variables.Count);
            Assert.Equal("1900-01-01 00:00:00", variables["test"].ToString());
        }

        [Fact]
        public void TestDeterministicMode_GenSymbol()
        {
            string variableName = "test";
            string format = "yyyy-MM-dd HH:mm:ss";
            bool utc = false;
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "format", JExtensions.ToJsonString(format) },
                { "utc", JExtensions.ToJsonString(utc) }
            };
            GeneratedSymbol deferredConfig = new(variableName, "NowMacro", jsonParameters, "string");

            IVariableCollection variables = new VariableCollection();
            NowMacro macro = new();

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, deferredConfig);

            Assert.Equal(1, variables.Count);
            Assert.Equal("1900-01-01 00:00:00", variables["test"].ToString());
        }
    }
}
