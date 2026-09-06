// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Fakes;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    [TestClass]
    public class FakeMacroTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public FakeMacroTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [TestMethod]
        public void TestEvaluationOfFakeMacro()
        {
            string variableName = "myHelloMacro";
            string sourceVariable = "originalValue";

            FakeMacro macro = new();
            FakeMacroConfig macroConfig = new(macro, variableName, sourceVariable, "name to greet");
            IVariableCollection variables = new VariableCollection();
            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            string newValue = (string)variables[variableName];
            Assert.HasCount(1, variables);
            Assert.AreEqual("Hello name to greet!", newValue);
        }

        [TestMethod]
        public void TestDependencyResolutionOfFakeMacro()
        {
            string variableName = "myHelloMacro";
            string sourceVariable = "originalValue";

            FakeMacro macro = new();
            FakeMacroConfig macroConfig = new(macro, variableName, sourceVariable);

            IVariableCollection variables = new VariableCollection();
            string sourceValue = "dependentMacro";
            variables[sourceVariable] = sourceValue;

            macroConfig.ResolveSymbolDependencies(variables.Select(v => v.Key).ToList());

            Assert.HasCount(1, macroConfig.Dependencies);
            Assert.AreEqual(sourceVariable, macroConfig.Dependencies.First());
        }

        [TestMethod]
        public void TestExceptionOnAccessToDependenciesOfFakeMacro()
        {
            string variableName = "myHelloMacro";
            string sourceVariable = "originalValue";

            FakeMacro macro = new();
            FakeMacroConfig macroConfig = new(macro, variableName, sourceVariable);

            var exception = Assert.ThrowsExactly<ArgumentException>(() => macroConfig.Dependencies);
            Assert.AreEqual("The method 'PopulateMacroConfigDependency' must be called prior 'Dependencies' property reading.", exception.Message);
        }
    }
}
