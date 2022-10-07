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

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            long randomValue = (int)variables[variableName];
            Assert.True(randomValue >= low);

            if (high.HasValue)
            {
                Assert.True(randomValue <= high);
            }
        }

        [Theory(DisplayName = nameof(TestRandomDeferredConfig))]
        [InlineData(1, 10)]
        [InlineData(0, null)]
        [InlineData(-1, 1)]
        [InlineData(10000, null)]
        [InlineData(123, 123)]
        public void TestRandomDeferredConfig(int low, int? high)
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

            GeneratedSymbol deferredConfig = new(variableName, "RandomMacro", jsonParameters);
            IVariableCollection variables = new VariableCollection();

            RandomMacro macro = new();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);
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

            Assert.Equal(1, variables.Count);
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

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, deferredConfig);

            Assert.Equal(1, variables.Count);
            Assert.Equal(10, variables["test"]);
        }
    }
}
