using System;
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
    public class JoinMacroTest : TestBase
    {
        [Theory(DisplayName = nameof(TestJoinConstantAndReferenceSymbolConfig))]
        [InlineData(",")]
        [InlineData("")]
        [InlineData(null)]
        public void TestJoinConstantAndReferenceSymbolConfig(string separator)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string constantValue = "constantValue";

            List<KeyValuePair<string,string>> definitions = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string,string>("const",constantValue),
                new KeyValuePair<string,string>("ref",referenceSymbolName)
            };

            JoinMacroConfig macroConfig = new JoinMacroConfig(variableName, null,definitions,separator);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new RunnableProjectGenerator.ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            Parameter referenceParam = new Parameter
            {
                IsVariable = true,
                Name = referenceSymbolName
            };

            variables[referenceSymbolName] = referenceSymbolValue;
            setter(referenceParam, referenceSymbolValue);

            JoinMacro macro = new JoinMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);

            Assert.True(parameters.TryGetParameterDefinition(variableName, out ITemplateParameter convertedParam));

            string convertedValue = (string) parameters.ResolvedValues[convertedParam];
            string expectedValue = string.Join(separator, constantValue, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }

        [Theory(DisplayName = nameof(TestDeferredJoinConfig))]
        [InlineData(",")]
        [InlineData("")]
        [InlineData(null)]
        public void TestDeferredJoinConfig(string separator)
        {
            string variableName = "joinedParameter";
            string referenceSymbolName = "referenceSymbol";
            string referenceSymbolValue = "referenceValue";
            string constantValue = "constantValue";

            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            string symbols =
                $"[ {{\"type\":\"const\" , \"value\":\"{constantValue}\"  }}, {{\"type\":\"ref\" , \"value\":\"{referenceSymbolName}\"  }} ]";
            jsonParameters.Add("symbols", JArray.Parse(symbols));
            if (!string.IsNullOrEmpty(separator))
            {
                jsonParameters.Add("separator", separator);
            }

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("JoinMacro", null, variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new RunnableProjectGenerator.ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            Parameter referenceParam = new Parameter
            {
                IsVariable = true,
                Name = referenceSymbolName
            };

            variables[referenceSymbolName] = referenceSymbolValue;
            setter(referenceParam, referenceSymbolValue);

            JoinMacro macro = new JoinMacro();
            IMacroConfig realConfig = macro.CreateConfig(EngineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, realConfig, parameters, setter);

            Assert.True(parameters.TryGetParameterDefinition(variableName, out ITemplateParameter convertedParam));

            string convertedValue = (string) parameters.ResolvedValues[convertedParam];
            string expectedValue = string.Join(separator, constantValue, referenceSymbolValue);
            Assert.Equal(convertedValue, expectedValue);
        }
    }
}
