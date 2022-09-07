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
    public class JoinMacroTest : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public JoinMacroTest(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Theory(DisplayName = nameof(TestJoinConstantAndReferenceSymbolConfig))]
        [InlineData(",", true)]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData(",", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TestJoinConstantAndReferenceSymbolConfig(string separator, bool removeEmptyValues)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string referenceEmptySymbolName = "referenceEmptySymbol";
            string constantValue = "constantValue";

            List<(JoinMacroConfig.JoinType, string)> definitions = new()
            {
                (JoinMacroConfig.JoinType.Const, constantValue),
                (JoinMacroConfig.JoinType.Ref, referenceEmptySymbolName),
                (JoinMacroConfig.JoinType.Ref, referenceSymbolName)
            };

            JoinMacro macro = new();
            JoinMacroConfig macroConfig = new(macro, variableName, null, definitions, separator, removeEmptyValues);

            IVariableCollection variables = new VariableCollection
            {
                [referenceSymbolName] = referenceSymbolValue
            };

            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            string convertedValue = (string)variables[variableName];
            string expectedValue =
                removeEmptyValues ?
                string.Join(separator, constantValue, referenceSymbolValue) :
                string.Join(separator, constantValue, null, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }

        [Theory(DisplayName = nameof(TestDeferredJoinConfig))]
        [InlineData(",")]
        [InlineData("")]
        public void TestDeferredJoinConfig(string separator)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string constantValue = "constantValue";

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            string symbols =
                $"[ {{\"type\":\"const\" , \"value\":\"{constantValue}\"  }}, {{\"type\":\"ref\" , \"value\":\"{referenceSymbolName}\"  }} ]";
            jsonParameters.Add("symbols", symbols);
            if (separator != null)
            {
                jsonParameters.Add("separator", JExtensions.ToJsonString(separator));
            }
            GeneratedSymbol deferredConfig = new(variableName, "JoinMacro", jsonParameters);

            IVariableCollection variables = new VariableCollection
            {
                [referenceSymbolName] = referenceSymbolValue
            };

            JoinMacro macro = new();
            IMacroConfig realConfig = macro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);

            string convertedValue = (string)variables[variableName];
            string expectedValue = string.Join(separator, constantValue, referenceSymbolValue);
            Assert.Equal(expectedValue, convertedValue);
        }
    }
}
