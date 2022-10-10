// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class CoalesceMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public CoalesceMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Theory]
        [InlineData(null, null, null, null)]
        [InlineData("", "", null, "")]
        [InlineData(null, "fallback", null, "fallback")]
        [InlineData("", "fallback", null, "")]
        [InlineData("def", "fallback", "def", "fallback")]
        [InlineData("def", "fallback", "", "def")]
        public void CoalesceMacroTest(string? sourceValue, string? fallbackValue, string? defaultValue, string? expectedResult)
        {
            CoalesceMacro macro = new();
            CoalesceMacroConfig macroConfig = new(macro, "test", "string", "varA", defaultValue, "varB");

            VariableCollection variables = new();
            if (sourceValue != null)
            {
                variables["varA"] = sourceValue;
            }
            if (fallbackValue != null)
            {
                variables["varB"] = fallbackValue;
            }

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            if (expectedResult == null)
            {
                Assert.False(variables.ContainsKey("test"));
            }
            else
            {
                Assert.Equal(expectedResult, variables["test"]);
            }
        }

        [Theory]
        [InlineData(null, null, null, null)]
        [InlineData("", "", null, "")]
        [InlineData(null, "fallback", null, "fallback")]
        [InlineData("", "fallback", null, "")]
        [InlineData("def", "fallback", "def", "fallback")]
        [InlineData("def", "fallback", "", "def")]
        public void GeneratedSymbolTest(string? sourceValue, string? fallbackValue, string? defaultValue, string? expectedResult)
        {
            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "sourceVariableName", JExtensions.ToJsonString("varA") },
                { "fallbackVariableName", JExtensions.ToJsonString("varB") }
            };
            if (defaultValue != null)
            {
                jsonParameters["defaultValue"] = JExtensions.ToJsonString(defaultValue);
            }

            VariableCollection variables = new();
            if (sourceValue != null)
            {
                variables["varA"] = sourceValue;
            }
            if (fallbackValue != null)
            {
                variables["varB"] = fallbackValue;
            }

            macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters));

            if (expectedResult == null)
            {
                Assert.False(variables.ContainsKey("test"));
            }
            else
            {
                Assert.Equal(expectedResult, variables["test"]);
            }
        }

        [Fact]
        public void InvalidConfigurationTest_Source()
        {
            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "fallbackVariableName", JExtensions.ToJsonString("varB") }
            };
            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'coalesce' should have 'sourceVariableName' property defined.", ex.Message);
        }

        [Fact]
        public void InvalidConfigurationTest_Fallback()
        {
            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "sourceVariableName", JExtensions.ToJsonString("varA") }
            };
            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.Throws<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters)));
            Assert.Equal("Generated symbol 'test' of type 'coalesce' should have 'fallbackVariableName' property defined.", ex.Message);
        }
    }
}
