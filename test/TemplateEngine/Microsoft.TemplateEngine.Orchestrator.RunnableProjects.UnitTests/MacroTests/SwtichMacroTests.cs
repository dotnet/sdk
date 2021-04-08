using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;
using Newtonsoft.Json.Linq;
using Xunit;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class SwtichMacroTests : TestBase
    {
        [Fact(DisplayName = nameof(TestSwitchConfig))]
        public void TestSwitchConfig()
        {
            string variableName = "mySwitchVar";
            string evaluator = "C++";
            string dataType = "string";
            string expectedValue = "this one";
            IList<KeyValuePair<string, string>> switches = new List<KeyValuePair<string, string>>();
            switches.Add(new KeyValuePair<string, string>("(3 > 10)", "three greater than ten - false"));
            switches.Add(new KeyValuePair<string, string>("(false)", "false value"));
            switches.Add(new KeyValuePair<string, string>("(10 > 0)", expectedValue));
            switches.Add(new KeyValuePair<string, string>("(5 > 4)", "not this one"));
            SwitchMacroConfig macroConfig = new SwitchMacroConfig(variableName, evaluator, dataType, switches);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            SwitchMacro macro = new SwitchMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);
            ITemplateParameter resultParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out resultParam));
            string resultValue = (string)parameters.ResolvedValues[resultParam];
            Assert.Equal(resultValue, expectedValue);
        }

        [Fact(DisplayName = nameof(TestSwitchDeferredConfig))]
        public void TestSwitchDeferredConfig()
        {
            string variableName = "mySwitchVar";
            string evaluator = "C++";
            string dataType = "string";
            string expectedValue = "this one";
            string switchCases = @"[
                {
                    'condition': '(3 > 10)',
                    'value': 'three greater than ten'
                },
                {
                    'condition': '(false)',
                    'value': 'false value'
                },
                {
                    'condition': '(10 > 0)',
                    'value': '" + expectedValue + @"'
                },
                {
                    'condition': '(5 > 4)',
                    'value': 'not this one'
                }
            ]";

            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            jsonParameters.Add("evaluator", evaluator);
            jsonParameters.Add("datatype", dataType);
            jsonParameters.Add("cases", JArray.Parse(switchCases));

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("SwitchMacro", null, variableName, jsonParameters);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            SwitchMacro macro = new SwitchMacro();
            IMacroConfig realConfig = macro.CreateConfig(EngineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter resultParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out resultParam));
            string resultValue = (string)parameters.ResolvedValues[resultParam];
            Assert.Equal(resultValue, expectedValue);
        }
    }
}
