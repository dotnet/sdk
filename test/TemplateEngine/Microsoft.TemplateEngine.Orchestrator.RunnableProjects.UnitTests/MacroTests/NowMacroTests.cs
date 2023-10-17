// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;

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

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);
            Assert.IsType<string>(variables[variableName]);
            string macroNowString = (string)variables[variableName]!;
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.UtcNow);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.True(difference.TotalSeconds < 10);
        }

        [Fact]
        public void GeneratedSymbolTest()
        {
            string variableName = "nowString";
            string format = string.Empty;
            bool utc = false;
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "format", JExtensions.ToJsonString(format) },
                { "utc", JExtensions.ToJsonString(utc) }
            };
            GeneratedSymbol symbol = new(variableName, "now", jsonParameters);
            IVariableCollection variables = new VariableCollection();

            NowMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
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

            Assert.Single(variables);
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

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, macro.CreateConfig(_engineEnvironmentSettings, deferredConfig));

            Assert.Single(variables);
            Assert.Equal("1900-01-01 00:00:00", variables["test"].ToString());
        }

        [Fact]
        [Obsolete("IMacro.EvaluateConfig is obsolete")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "nowString";

            NowMacro macro = new();
            NowMacroConfig macroConfig = new(macro, variableName, "yyyy-MM-dd HH:mm:ss", false);
            Assert.Equal("string", macroConfig.DataType);

            IVariableCollection variables = new VariableCollection();

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            Assert.IsType<string>(variables[variableName]);
            string macroNowString = (string)variables[variableName]!;
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.Now);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.True(difference.TotalSeconds < 10);
        }

        [Fact]
        public void DefaultConfigurationTest()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            NowMacro macro = new();
            GeneratedSymbol symbol = new(variableName, "now", jsonParameters);
            NowMacroConfig config = new(macro, symbol);

            Assert.Null(config.Format);
            Assert.False(config.Utc);
        }
    }
}
