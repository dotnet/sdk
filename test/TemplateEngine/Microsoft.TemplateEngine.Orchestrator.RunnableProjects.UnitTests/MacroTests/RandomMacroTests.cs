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
    public class RandomMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RandomMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Theory(DisplayName = nameof(TestRandomConfig))]
        [InlineData(0, 100)]
        [InlineData(-1000, -900)]
        [InlineData(50, 50)]
        [InlineData(1000, null)]
        [InlineData(0, null)]
        public void TestRandomConfig(int low, int? high)
        {
            string variableName = "myRnd";
            RandomMacro macro = new();
            RandomMacroConfig macroConfig = new(macro, variableName, null, low, high);

            IVariableCollection variables = new VariableCollection();

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            long randomValue = (int)variables[variableName];
            Assert.True(randomValue >= low);

            if (high.HasValue)
            {
                Assert.True(randomValue <= high);
            }
        }

        [Theory]
        [InlineData(1, 10)]
        [InlineData(0, null)]
        [InlineData(-1, 1)]
        [InlineData(10000, null)]
        [InlineData(123, 123)]
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
            Assert.True(randomValue >= low);

            if (high.HasValue)
            {
                Assert.True(randomValue <= high);
            }
        }

        [Fact]
        public void TestDeterministicMode()
        {
            IVariableCollection variables = new VariableCollection();
            RandomMacro macro = new();
            RandomMacroConfig config = new(macro, "test", "integer", 10, 100);
            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, config);

            Assert.Single(variables);
            Assert.Equal(10, variables["test"]);
        }

        [Fact]
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

            Assert.Single(variables);
            Assert.Equal(10, variables["test"]);
        }

        [Fact]
        public void InvalidConfigurationTest()
        {
            RandomMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);

            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "random", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'random' should have 'low' property defined.", ex.Message);
        }

        [Fact]
        public void DefaultConfigurationTest()
        {
            RandomMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "low", JExtensions.ToJsonString(0) }
            };
            RandomMacroConfig config = new(macro, new GeneratedSymbol("test", "random", jsonParameters));
            Assert.Equal(int.MaxValue, config.High);
        }
    }
}
