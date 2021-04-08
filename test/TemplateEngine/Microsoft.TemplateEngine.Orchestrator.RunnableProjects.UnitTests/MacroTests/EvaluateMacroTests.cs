using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;
using Xunit;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    public class EvaluateMacroTests : TestBase
    {
        [Theory(DisplayName = nameof(TestEvaluateConfig))]
        [InlineData("(7 > 3)", true)]
        [InlineData("(2 == 1)", false)]
        public void TestEvaluateConfig(string predicate, bool expectedResult)
        {
            string variableName = "myPredicate";
            string evaluator = "C++";
            EvaluateMacroConfig macroConfig = new EvaluateMacroConfig(variableName, null, predicate, evaluator);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            EvaluateMacro macro = new EvaluateMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);
            ITemplateParameter resultParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out resultParam));
            bool resultValue = (bool)parameters.ResolvedValues[resultParam];
            Assert.Equal(resultValue, expectedResult);
        }
    }
}
