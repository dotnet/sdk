// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

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
        public void TestDeterministicMode()
        {
            IVariableCollection variables = new VariableCollection();
            GeneratePortNumberMacro macro = new();
            GeneratePortNumberConfig config = new(macro, "test", "integer", 3000, 4000, 5000);
            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, config);

            Assert.Equal(1, variables.Count);
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

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, deferredConfig);
            Assert.Equal(1, variables.Count);
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

            macro.EvaluateDeterministically(_engineEnvironmentSettings, variables, deferredConfig);
            Assert.Equal(1, variables.Count);
            Assert.Equal(GeneratePortNumberConfig.LowPortDefault, variables["test"]);
        }
    }
}
