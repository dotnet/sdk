// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
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
    public class GuidMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public GuidMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(TestGuidConfig))]
        public void TestGuidConfig()
        {
            string variableName = "TestGuid";
            IMacroConfig macroConfig = new GuidMacroConfig(variableName, "string", string.Empty, null);

            IVariableCollection variables = new VariableCollection();

            GuidMacro guidMacro = new GuidMacro();
            guidMacro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            ValidateGuidMacroCreatedParametersWithResolvedValues(variableName, variables);
        }

        [Fact(DisplayName = nameof(TestDeferredGuidConfig))]
        public void TestDeferredGuidConfig()
        {
            Dictionary<string, JToken> jsonParameters = new();
            string variableName = "myGuid1";
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("GuidMacro", "string", variableName, jsonParameters);

            GuidMacro guidMacro = new GuidMacro();
            IVariableCollection variables = new VariableCollection();

            IMacroConfig realConfig = guidMacro.CreateConfig(_engineEnvironmentSettings, deferredConfig);
            guidMacro.EvaluateConfig(_engineEnvironmentSettings, variables, realConfig);
            ValidateGuidMacroCreatedParametersWithResolvedValues(variableName, variables);
        }

        private static void ValidateGuidMacroCreatedParametersWithResolvedValues(string variableName, IVariableCollection variables)
        {
            Assert.True(variables.ContainsKey(variableName));
            Assert.NotNull(variables[variableName]);
            Guid paramValue = Guid.Parse((string)variables[variableName]!);

            // check that all the param name variants were created, and their values all resolve to the same guid.
            string guidFormats = GuidMacroConfig.DefaultFormats;
            for (int i = 0; i < guidFormats.Length; ++i)
            {
                string otherFormatVariableName = variableName + "-" + guidFormats[i];
                Assert.NotNull(variables[otherFormatVariableName]);
                Guid testValue = Guid.Parse((string)variables[otherFormatVariableName]!);
                Assert.Equal(paramValue, testValue);

                // Test the new formats - that distinguish upper and lower case by tags that are
                //  distinguishable regardless of casing comparison
                otherFormatVariableName =
                    variableName +
                    (char.IsUpper(guidFormats[i]) ? GuidMacroConfig.UpperCaseDenominator : GuidMacroConfig.LowerCaseDenominator) +
                    guidFormats[i];

                string resolvedValue = (string)variables[otherFormatVariableName]!;
                testValue = Guid.Parse((string)variables[otherFormatVariableName]!);
                Assert.Equal(paramValue, testValue);
                Assert.Equal(char.IsUpper(guidFormats[i]), char.IsUpper(resolvedValue.First(char.IsLetter)));
            }
        }

        [Fact]
        public void TestDefaultFormatIsCaseSensetive()
        {
            string paramNameLower = "TestGuidLower";
            IMacroConfig macroConfigLower = new GuidMacroConfig(paramNameLower, "string", string.Empty, "n");
            string paramNameUpper = "TestGuidUPPER";
            IMacroConfig macroConfigUpper = new GuidMacroConfig(paramNameUpper, "string", string.Empty, "N");

            IVariableCollection variables = new VariableCollection();

            GuidMacro guidMacro = new GuidMacro();
            guidMacro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfigLower);
            guidMacro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfigUpper);

            Assert.True(variables.ContainsKey(paramNameLower));
            Assert.NotNull(variables[paramNameLower]);
            Assert.All(((string)variables[paramNameLower]!).ToCharArray(), (c) =>
            {
                Assert.True(char.IsLower(c) || char.IsDigit(c));
            });

            Assert.True(variables.ContainsKey(paramNameUpper));
            Assert.NotNull(variables[paramNameUpper]);
            Assert.All(((string)variables[paramNameUpper]!).ToCharArray(), (c) =>
            {
                Assert.True(char.IsUpper(c) || char.IsDigit(c));
            });
        }
    }
}
