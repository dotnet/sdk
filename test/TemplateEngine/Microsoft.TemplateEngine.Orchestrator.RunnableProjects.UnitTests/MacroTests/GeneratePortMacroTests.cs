// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    [TestClass]
    public class GeneratePortMacroTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public GeneratePortMacroTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [TestMethod]
        public void BasicMacroTest()
        {
            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();
            GeneratePortNumberConfig config = new(macro, "test", "integer", 3000, 4000, 5000);
            macro.Evaluate(_engineEnvironmentSettings, variables, config);

            Assert.ContainsSingle(variables);

            int result = (int)variables["test"];

            Assert.IsGreaterThanOrEqualTo(4000, result);
            Assert.IsLessThanOrEqualTo(5000, result);
        }

        [TestMethod]
        public void GeneratedSymbolTest()
        {
            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "low", JExtensions.ToJsonString(4000) },
                { "high", JExtensions.ToJsonString(5000) },
                { "fallback", JExtensions.ToJsonString(3000) },
            };
            GeneratedSymbol symbol = new("test", macro.Type, jsonParameters);
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);

            Assert.ContainsSingle(variables);

            int result = (int)variables["test"];

            Assert.IsGreaterThanOrEqualTo(4000, result);
            Assert.IsLessThanOrEqualTo(5000, result);
        }

        [TestMethod]
        public void TestDeterministicMode()
        {
            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();
            GeneratePortNumberConfig config = new(macro, "test", "integer", 3000, 4000, 5000);
            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, config);

            Assert.ContainsSingle(variables);
            Assert.AreEqual(4000, variables["test"]);
        }

        [TestMethod]
        public void TestDeterministicMode_GenSymbol()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "low", JExtensions.ToJsonString(4000) },
                { "high", JExtensions.ToJsonString(5000) }
            };

            GeneratedSymbol deferredConfig = new(variableName, "port", jsonParameters, "integer");

            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, macro.CreateConfig(_engineEnvironmentSettings, deferredConfig));
            Assert.ContainsSingle(variables);
            Assert.AreEqual(4000, variables["test"]);
        }

        [TestMethod]
        public void TestDeterministicMode_GenSymbol_Default()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);

            GeneratedSymbol deferredConfig = new(variableName, "port", jsonParameters, "integer");

            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, macro.CreateConfig(_engineEnvironmentSettings, deferredConfig));
            Assert.ContainsSingle(variables);
            Assert.AreEqual(GeneratePortNumberConfig.LowPortDefault, variables["test"]);
        }

        [TestMethod]
        public void DefaultConfigurationTest()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            GeneratePortNumberMacro macro = new();
            GeneratedSymbol symbol = new(variableName, "port", jsonParameters, "integer");
            GeneratePortNumberConfig config = new(NullLogger.Instance, macro, symbol);

            Assert.AreEqual(1024, config.Low);
            Assert.AreEqual(65535, config.High);
            Assert.AreEqual(0, config.Fallback);
        }
    }
}
