using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;
using Newtonsoft.Json.Linq;
using Xunit;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class CaseChangeMacroTests : TestBase
    {
        [Fact(DisplayName = nameof(TestCaseChangeToLowerConfig))]
        public void TestCaseChangeToLowerConfig()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";
            bool toLower = true;

            CaseChangeMacroConfig macroConfig = new CaseChangeMacroConfig(variableName, null, sourceVariable, toLower);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            string sourceValue = "Original Value SomethingCamelCase";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            CaseChangeMacro macro = new CaseChangeMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter convertedParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out convertedParam));
            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            Assert.Equal(convertedValue, sourceValue.ToLower());
        }

        [Fact(DisplayName = nameof(TestCaseChangeToUpperConfig))]
        public void TestCaseChangeToUpperConfig()
        {
            string variableName = "myString";
            string sourceVariable = "sourceString";
            bool toLower = false;

            CaseChangeMacroConfig macroConfig = new CaseChangeMacroConfig(variableName, null, sourceVariable, toLower);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            string sourceValue = "Original Value SomethingCamelCase";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            CaseChangeMacro macro = new CaseChangeMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter convertedParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out convertedParam));
            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            Assert.Equal(convertedValue, sourceValue.ToUpper());
        }

        [Fact(DisplayName = nameof(TestDeferredCaseChangeConfig))]
        public void TestDeferredCaseChangeConfig()
        {
            string variableName = "myString";

            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            string sourceVariable = "sourceString";
            jsonParameters.Add("source", sourceVariable);
            jsonParameters.Add("toLower", false);
            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("CaseChangeMacro", null, variableName, jsonParameters);

            CaseChangeMacro macro = new CaseChangeMacro();
            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            string sourceValue = "Original Value SomethingCamelCase";
            Parameter sourceParam = new Parameter
            {
                IsVariable = true,
                Name = sourceVariable
            };

            variables[sourceVariable] = sourceValue;
            setter(sourceParam, sourceValue);

            IMacroConfig realConfig = macro.CreateConfig(EngineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter convertedParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out convertedParam));
            string convertedValue = (string)parameters.ResolvedValues[convertedParam];
            Assert.Equal(convertedValue, sourceValue.ToUpper());
        }
    }
}
