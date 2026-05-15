// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Fakes;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class FakeMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public FakeMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestEvaluationOfFakeMacro))]
        public void TestEvaluationOfFakeMacro()
        {
            string variableName = "myHelloMacro";
            string sourceVariable = "originalValue";

            FakeMacro macro = new();
            FakeMacroConfig macroConfig = new(macro, variableName, sourceVariable, "name to greet");
            IVariableCollection variables = new VariableCollection();
            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            string newValue = (string)variables[variableName];
            Assert.True(variables.Count == 1);
            Assert.Equal("Hello name to greet!", newValue);
        }

        [Fact(DisplayName = nameof(TestDependencyResolutionOfFakeMacro))]
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

            Assert.True(macroConfig.Dependencies.Count == 1);
            Assert.Equal(sourceVariable, macroConfig.Dependencies.First());
        }

        [Fact(DisplayName = nameof(TestExceptionOnAccessToDependenciesOfFakeMacro))]
        public void TestExceptionOnAccessToDependenciesOfFakeMacro()
        {
            string variableName = "myHelloMacro";
            string sourceVariable = "originalValue";

            FakeMacro macro = new();
            FakeMacroConfig macroConfig = new(macro, variableName, sourceVariable);

            var exception = Assert.Throws<ArgumentException>(() => macroConfig.Dependencies);
            Assert.Equal("The method 'PopulateMacroConfigDependency' must be called prior 'Dependencies' property reading.", exception.Message);
        }
    }
}
