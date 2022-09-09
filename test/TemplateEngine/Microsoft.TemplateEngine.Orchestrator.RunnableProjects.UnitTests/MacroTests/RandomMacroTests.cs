// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class RandomMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RandomMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
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
            RandomMacroConfig macroConfig = new RandomMacroConfig(variableName, null, low, high);

            IVariableCollection variables = new VariableCollection();

            RandomMacro macro = new RandomMacro();
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
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>
            {
                { "low", low }
            };
            if (high.HasValue)
            {
                jsonParameters.Add("high", high);
            }

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("RandomMacro", null, variableName, jsonParameters);
            IVariableCollection variables = new VariableCollection();

            RandomMacro macro = new RandomMacro();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);
            long randomValue = (int)variables[variableName];
            Assert.True(randomValue >= low);

            if (high.HasValue)
            {
                Assert.True(randomValue <= high);
            }
        }
    }
}
