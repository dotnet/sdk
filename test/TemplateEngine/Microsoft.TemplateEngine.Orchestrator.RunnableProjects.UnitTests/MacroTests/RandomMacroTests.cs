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
    public class RandomMacroTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RandomMacroTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [TestMethod]
        [DataRow(0, 100)]
        [DataRow(-1000, -900)]
        [DataRow(50, 50)]
        [DataRow(1000, null)]
        [DataRow(0, null)]
        public void TestRandomConfig(int low, int? high)
        {
            string variableName = "myRnd";
            RandomMacro macro = new();
            RandomMacroConfig macroConfig = new(macro, variableName, null, low, high);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            long randomValue = (int)variables[variableName];
            Assert.IsGreaterThanOrEqualTo(low, randomValue);

            if (high.HasValue)
            {
                Assert.IsLessThanOrEqualTo((long)high.Value, randomValue);
            }
        }

        [TestMethod]
        [DataRow(1, 10)]
        [DataRow(0, null)]
        [DataRow(-1, 1)]
        [DataRow(10000, null)]
        [DataRow(123, 123)]
        public void GeneratedSymbolTest(int low, int? high)
        {
            string variableName = "myRnd";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "low", JExtensions.ToJsonString(low) }
            };
            if (high.HasValue)
            {
                jsonParameters.Add("high", JExtensions.ToJsonString(high));
            }

            GeneratedSymbol symbol = new(variableName, "random", jsonParameters);
            IVariableCollection variables = new VariableCollection();

            RandomMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            long randomValue = (int)variables[variableName];
            Assert.IsGreaterThanOrEqualTo(low, randomValue);

            if (high.HasValue)
            {
                Assert.IsLessThanOrEqualTo((long)high.Value, randomValue);
            }
        }

        [TestMethod]
        public void TestDeterministicMode()
        {
            IVariableCollection variables = new VariableCollection();
            RandomMacro macro = new();
            RandomMacroConfig config = new(macro, "test", "integer", 10, 100);
            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, config);

            Assert.ContainsSingle(variables);
            Assert.AreEqual(10, variables["test"]);
        }

        [TestMethod]
        public void TestDeterministicMode_GenSymbol()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "low", JExtensions.ToJsonString(10) },
                { "high", JExtensions.ToJsonString(100) }
            };
            GeneratedSymbol deferredConfig = new(variableName, "random", jsonParameters, "integer");

            IVariableCollection variables = new VariableCollection();
            RandomMacro macro = new();

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, macro.CreateConfig(_engineEnvironmentSettings, deferredConfig));

            Assert.ContainsSingle(variables);
            Assert.AreEqual(10, variables["test"]);
        }

        [TestMethod]
        public void InvalidConfigurationTest()
        {
            RandomMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.ThrowsExactly<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "random", jsonParameters)));
            Assert.AreEqual("Generated symbol 'test' of type 'random' should have 'low' property defined.", ex.Message);
        }

        [TestMethod]
        public void DefaultConfigurationTest()
        {
            RandomMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "low", JExtensions.ToJsonString(0) }
            };
            RandomMacroConfig config = new(macro, new GeneratedSymbol("test", "random", jsonParameters));
            Assert.AreEqual(int.MaxValue, config.High);
        }
    }
}
