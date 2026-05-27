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
    public class GeneratePortMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public GeneratePortMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact]
        public void BasicMacroTest()
        {
            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();
            GeneratePortNumberConfig config = new(macro, "test", "integer", 3000, 4000, 5000);
            macro.Evaluate(_engineEnvironmentSettings, variables, config);

            Assert.Single(variables);

            int result = (int)variables["test"];

            Assert.True(result >= 4000);
            Assert.True(result <= 5000);
        }

        [Fact]
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

            Assert.Single(variables);

            int result = (int)variables["test"];

            Assert.True(result >= 4000);
            Assert.True(result <= 5000);
        }

        [Fact]
        public void TestDeterministicMode()
        {
            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();
            GeneratePortNumberConfig config = new(macro, "test", "integer", 3000, 4000, 5000);
            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, config);

            Assert.Single(variables);
            Assert.Equal(4000, variables["test"]);
        }

        [Fact]
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
            Assert.Single(variables);
            Assert.Equal(4000, variables["test"]);
        }

        [Fact]
        public void TestDeterministicMode_GenSymbol_Default()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);

            GeneratedSymbol deferredConfig = new(variableName, "port", jsonParameters, "integer");

            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, macro.CreateConfig(_engineEnvironmentSettings, deferredConfig));
            Assert.Single(variables);
            Assert.Equal(GeneratePortNumberConfig.LowPortDefault, variables["test"]);
        }

        [Fact]
        public void DefaultConfigurationTest()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            GeneratePortNumberMacro macro = new();
            GeneratedSymbol symbol = new(variableName, "port", jsonParameters, "integer");
            GeneratePortNumberConfig config = new(NullLogger.Instance, macro, symbol);

            Assert.Equal(1024, config.Low);
            Assert.Equal(65535, config.High);
            Assert.Equal(0, config.Fallback);
        }
    }
}
