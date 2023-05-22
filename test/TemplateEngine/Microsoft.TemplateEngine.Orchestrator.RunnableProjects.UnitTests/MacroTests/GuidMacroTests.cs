// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class GuidMacroTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public GuidMacroTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact]
        public void Test_SimpleMacro()
        {
            string variableName = "TestGuid";
            GuidMacroConfig macroConfig = new GuidMacroConfig(variableName, "string", string.Empty, null);

            IVariableCollection variables = new VariableCollection();

            GuidMacro guidMacro = new();
            guidMacro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);
            ValidateGuidMacroCreatedParametersWithResolvedValues(variableName, variables);
        }

        [Fact]
        public void GeneratedSymbolTest()
        {
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            string variableName = "myGuid1";
            GeneratedSymbol symbol = new(variableName, "GuidMacro", jsonParameters);

            GuidMacro guidMacro = new();
            IVariableCollection variables = new VariableCollection();
            guidMacro.Evaluate(_engineEnvironmentSettings, variables, symbol);
            ValidateGuidMacroCreatedParametersWithResolvedValues(variableName, variables);
        }

        [Fact]
        [Obsolete("IMacro.EvaluateConfig is obsolete")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "TestGuid";
            GuidMacroConfig macroConfig = new GuidMacroConfig(variableName, "string", string.Empty, null);

            IVariableCollection variables = new VariableCollection();

            GuidMacro guidMacro = new();
            guidMacro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);
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
            GuidMacroConfig macroConfigLower = new GuidMacroConfig(paramNameLower, "string", string.Empty, "n");
            string paramNameUpper = "TestGuidUPPER";
            GuidMacroConfig macroConfigUpper = new GuidMacroConfig(paramNameUpper, "string", string.Empty, "N");

            IVariableCollection variables = new VariableCollection();

            GuidMacro guidMacro = new();
            guidMacro.Evaluate(_engineEnvironmentSettings, variables, macroConfigLower);
            guidMacro.Evaluate(_engineEnvironmentSettings, variables, macroConfigUpper);

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

        [Fact]
        public void TestDeterministicMode()
        {
            Guid deterministicModeValue = new("12345678-1234-1234-1234-1234567890AB");
            string variableName = "TestGuid";
            GuidMacroConfig macroConfig = new GuidMacroConfig(variableName, "string", "Nn", "n");

            IVariableCollection variables = new VariableCollection();

            GuidMacro guidMacro = new();
            guidMacro.EvaluateDeterministically(_engineEnvironmentSettings, variables, macroConfig);

            Assert.Equal(5, variables.Count);
            Assert.Equal(deterministicModeValue.ToString("n"), variables["TestGuid-n"].ToString());
            Assert.Equal(deterministicModeValue.ToString("n"), variables["TestGuid-lc-n"].ToString());
            Assert.Equal(deterministicModeValue.ToString("n").ToUpperInvariant(), variables["TestGuid-N"].ToString());
            Assert.Equal(deterministicModeValue.ToString("n").ToUpperInvariant(), variables["TestGuid-uc-N"].ToString());
            Assert.Equal(deterministicModeValue.ToString("n"), variables["TestGuid"].ToString());
        }

        [Fact]
        public void DefaultConfigurationTest()
        {
            string variableName = "test";
            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase);
            GuidMacro macro = new();
            GeneratedSymbol symbol = new(variableName, "guid", jsonParameters, "string");
            GuidMacroConfig config = new(macro, symbol);

            Assert.Equal("ndbpxNDPBX", config.Format);
            Assert.Equal("D", config.DefaultFormat);
        }
    }
}
