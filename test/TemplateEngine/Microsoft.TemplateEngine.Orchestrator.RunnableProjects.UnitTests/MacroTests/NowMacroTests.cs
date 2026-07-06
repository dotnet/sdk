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
    [TestClass]
    public class NowMacroTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public NowMacroTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        // tests regular Now configuration, and Utc = true
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("yyyy-MM-dd HH:mm:ss")]
        public void EvaluateNowConfig(string? format)
        {
            string variableName = "nowString";
            bool utc = true;

            NowMacro macro = new();
            NowMacroConfig macroConfig = new(macro, variableName, format, utc);
            Assert.AreEqual("string", macroConfig.DataType);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);
            Assert.IsExactInstanceOfType<string>(variables[variableName]);
            string macroNowString = (string)variables[variableName]!;
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.UtcNow);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.IsLessThan(10, difference.TotalSeconds);
        }

        [TestMethod]
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
            Assert.IsExactInstanceOfType<string>(variables[variableName]);
            string macroNowString = (string)variables[variableName]!;
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.Now);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.IsLessThan(10, difference.TotalSeconds);
        }

        [TestMethod]
        [DataRow(null, "string")]
        [DataRow("", "string")]
        [DataRow("string", "string")]
        [DataRow("date", "date")]
        public void EvaluateNowOverrideDatatypeInConfig(string? type, string expectedType)
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
            Assert.AreEqual(expectedType, (realConfig as NowMacroConfig)?.DataType);
        }

        [TestMethod]
        public void TestDeterministicMode()
        {
            IVariableCollection variables = new VariableCollection();
            NowMacro macro = new();
            NowMacroConfig config = new(macro, "test", "yyyy-MM-dd HH:mm:ss");
            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, config);

            Assert.ContainsSingle(variables);
            Assert.AreEqual("1900-01-01 00:00:00", variables["test"].ToString());
        }

        [TestMethod]
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

            Assert.ContainsSingle(variables);
            Assert.AreEqual("1900-01-01 00:00:00", variables["test"].ToString());
        }

        [TestMethod]
        [Obsolete("IMacro.EvaluateConfig is obsolete")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "nowString";

            NowMacro macro = new();
            NowMacroConfig macroConfig = new(macro, variableName, "yyyy-MM-dd HH:mm:ss", false);
            Assert.AreEqual("string", macroConfig.DataType);

            IVariableCollection variables = new VariableCollection();

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            Assert.IsExactInstanceOfType<string>(variables[variableName]);
            string macroNowString = (string)variables[variableName]!;
            DateTime macroNowTime = Convert.ToDateTime(macroNowString);

            TimeSpan difference = macroNowTime.Subtract(DateTime.Now);

            // 10 seconds is quite a lot of wiggle room, but should be fine, and safe.
            Assert.IsLessThan(10, difference.TotalSeconds);
        }

        [TestMethod]
        public void DefaultConfigurationTest()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            NowMacro macro = new();
            GeneratedSymbol symbol = new(variableName, "now", jsonParameters);
            NowMacroConfig config = new(macro, symbol);

            Assert.IsNull(config.Format);
            Assert.IsFalse(config.Utc);
        }
    }
}
