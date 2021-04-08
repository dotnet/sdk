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
    public class RandomMacroTests : TestBase
    {
        [Theory(DisplayName = nameof(TestRandomConfig))]
        [InlineData(0, 100)]
        [InlineData(-1000, -900)]
        [InlineData(50, 50)]
        [InlineData(1000, null)]
        [InlineData(0, null)]
        public void TestRandomConfig(int low, int? high)
        {
            string variableName = "myRnd";
            RandomMacroConfig macroConfig = new RandomMacroConfig(variableName, null, low, high);

            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            RandomMacro macro = new RandomMacro();
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, macroConfig, parameters, setter);

            ITemplateParameter valueParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out valueParam));
            long randomValue = (long)parameters.ResolvedValues[valueParam];
            Assert.True(randomValue >= low);

            if (high.HasValue)
            {
                Assert.True(randomValue <= high);
            }
        }

        [Theory(DisplayName = nameof(TestRandomDeferredConfig))]
        [InlineData(1, 10)]
        [InlineData(0, null)]
        [InlineData(-1, 1)]
        [InlineData(10000, null)]
        [InlineData(123, 123)]
        public void TestRandomDeferredConfig(int low, int? high)
        {
            string variableName = "myRnd";
            Dictionary<string, JToken> jsonParameters = new Dictionary<string, JToken>();
            jsonParameters.Add("low", low);
            if (high.HasValue)
            {
                jsonParameters.Add("high", high);
            }

            GeneratedSymbolDeferredMacroConfig deferredConfig = new GeneratedSymbolDeferredMacroConfig("RandomMacro", null, variableName, jsonParameters);
            IVariableCollection variables = new VariableCollection();
            IRunnableProjectConfig config = new SimpleConfigModel();
            IParameterSet parameters = new ParameterSet(config);
            ParameterSetter setter = MacroTestHelpers.TestParameterSetter(EngineEnvironmentSettings, parameters);

            RandomMacro macro = new RandomMacro();
            IMacroConfig realConfig = macro.CreateConfig(EngineEnvironmentSettings, deferredConfig);
            macro.EvaluateConfig(EngineEnvironmentSettings, variables, realConfig, parameters, setter);
            ITemplateParameter valueParam;
            Assert.True(parameters.TryGetParameterDefinition(variableName, out valueParam));
            long randomValue = (long)parameters.ResolvedValues[valueParam];
            Assert.True(randomValue >= low);

            if (high.HasValue)
            {
                Assert.True(randomValue <= high);
            }
        }
    }
}
