// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    [TestClass]
    public class ConstantMacroTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public ConstantMacroTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [TestMethod]
        public void TestConstantConfig()
        {
            string variableName = "myConstant";
            string value = "1048576";
            ConstantMacro macro = new();
            ConstantMacroConfig macroConfig = new(macro, null, variableName, value);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            Assert.AreEqual(value, variables[variableName]);
        }

        [TestMethod]
        public void GeneratedSymbolTest()
        {
            string variableName = "myConstant";
            string value = "1048576";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "value", JExtensions.ToJsonString(value) }
            };

            ConstantMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            Assert.AreEqual(value, variables[variableName]);
        }

        [TestMethod]
        public void GeneratedSymbolTest_Bool()
        {
            string variableName = "myConstant";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "value", JExtensions.ToJsonString(true) }
            };

            ConstantMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            Assert.AreEqual("true", variables[variableName]);
        }

        [TestMethod]
        public void GeneratedSymbolTest_Int()
        {
            string variableName = "myConstant";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "value", JExtensions.ToJsonString(1000) }
            };

            ConstantMacro macro = new();
            GeneratedSymbol symbol = new(variableName, macro.Type, jsonParameters);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            Assert.AreEqual("1000", variables[variableName]);
        }

        [TestMethod]
        [Obsolete("EvaluateConfig is deprecated")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "myConstant";
            string value = "1048576";
            ConstantMacro macro = new();
            ConstantMacroConfig macroConfig = new(macro, null, variableName, value);

            IVariableCollection variables = new VariableCollection();

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            Assert.AreEqual(value, variables[variableName]);
        }

        [TestMethod]
        public void InvalidConfigurationTest()
        {
            ConstantMacro macro = new();
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.ThrowsExactly<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "constant", jsonParameters)));
            Assert.AreEqual("Generated symbol 'test' of type 'constant' should have 'value' property defined.", ex.Message);
        }
    }
}
