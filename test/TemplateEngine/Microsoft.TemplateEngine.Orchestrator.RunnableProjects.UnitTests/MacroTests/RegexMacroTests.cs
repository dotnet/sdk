using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;
using Xunit;
using Newtonsoft.Json.Linq;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class RegexMacroTests : TestBase
    {
        [Fact(DisplayName = nameof(TestRegexMacro))]
        public void TestRegexMacro()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            IList<KeyValuePair<string, string>> steps = new List<KeyValuePair<string, string>>();
            steps.Add(new KeyValuePair<string, string>("2+", "3"));
            steps.Add(new KeyValuePair<string, string>("13", "Z"));
            RegexMacroConfig macroConfig = new RegexMacroConfig(variableName, null, sourceVariable, steps);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            string sourceValue = "QQQ121222112";
            string expectedValue = "QQQZZ1Z";

            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            RegexMacro macro = new RegexMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter newParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out newParam));
            string newValue = (string)parameters.ResolvedValues[newParam];
            Assert.Equal(newValue, expectedValue);
        }

        [Fact(DisplayName = nameof(TestRegexDeferredConfig))]
        public void TestRegexDeferredConfig()
        {
            string variableName = "myRegex";
            string sourceVariable = "originalValue";
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            jsonParameters.Add("source", sourceVariable);

            string jsonSteps = @"[
                { 
                    'regex': 'A',
                    'replacement': 'Z'
                }
            ]";
            jsonParameters.Add("steps", JArray.Parse(jsonSteps));

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("RegexMacro", "string", variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            string sourceValue = "ABCAABBCC";
            string expectedValue = "ZBCZZBBCC";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            RegexMacro macro = new RegexMacro();
            IMacroConfig realConfig = macro.CreateConfig(EngineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter newParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out newParam));
            string newValue = (string)parameters.ResolvedValues[newParam];
            Assert.Equal(newValue, expectedValue);
        }
    }
}
