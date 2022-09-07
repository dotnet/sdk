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
    public class RegexMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RegexMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestRegexMacro))]
        public void TestRegexMacro()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            List<(string, string)> steps = new()
            {
                ("2+", "3"),
                ("13", "Z")
            };

            RegexMacro macro = new();
            RegexMacroConfig macroConfig = new(macro, variableName, null, sourceVariable, steps);

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "QQQ121222112";
            string expectedValue = "QQQZZ1Z";

            variables[sourceVariable] = sourceValue;

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);

            string newValue = (string)variables[variableName];
            Assert.Equal(newValue, expectedValue);
        }

        [Fact(DisplayName = nameof(TestRegexDeferredConfig))]
        public void TestRegexDeferredConfig()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "source", JExtensions.ToJsonString(sourceVariable) }
            };

            string jsonSteps = @"[
                {
                    'regex': 'A',
                    'replacement': 'Z'
                }
            ]";
            jsonParameters.Add("steps", jsonSteps);

            GeneratedSymbol deferredConfig = new(variableName, "RegexMacro", jsonParameters, "string");

            IVariableCollection variables = new VariableCollection();

            string sourceValue = "ABCAABBCC";
            string expectedValue = "ZBCZZBBCC";

            variables[sourceVariable] = sourceValue;

            RegexMacro macro = new();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);
            string newValue = (string)variables[variableName];
            Assert.Equal(newValue, expectedValue);
        }
    }
}
