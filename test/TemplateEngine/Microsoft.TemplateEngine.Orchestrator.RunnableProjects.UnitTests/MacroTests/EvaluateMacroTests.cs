// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    [TestClass]
    public class EvaluateMacroTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public EvaluateMacroTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [TestMethod]
        [DataRow("(7 > 3)", true)]
        [DataRow("(2 == 1)", false)]
        public void TestEvaluateConfig(string predicate, bool expectedResult)
        {
            string variableName = "myPredicate";
            string evaluator = "C++";
            EvaluateMacroConfig macroConfig = new(variableName, "bool", predicate, evaluator);

            IVariableCollection variables = new VariableCollection();

            EvaluateMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);
            Assert.AreEqual(expectedResult, variables[variableName]);
        }

        [TestMethod(DisplayName = nameof(TestEvaluateConfig))]
        [DataRow("(Param == A)", "C++", "A|B", true)]
        [DataRow("(Param == C)", "C++", "A|B", false)]
        [DataRow("((Param == A) || (Param == B))", "C++", "A|B", true)]
        [DataRow("((Param == A) && (Param == B))", "C++", "A|B", true)]
        [DataRow("((Param == A) && (Param != B))", "C++", "A|B", false)]
        [DataRow("((Param == A) && (Param == C))", "C++", "A|B", false)]
        [DataRow("(Param == A)", "C++2", "A|B", true)]
        [DataRow("(Param == C)", "C++2", "A|B", false)]
        [DataRow("((Param == A) || (Param == B))", "C++2", "A|B", true)]
        [DataRow("((Param == A) && (Param == B))", "C++2", "A|B", true)]
        [DataRow("((Param == A) && (Param != B))", "C++2", "A|B", false)]
        [DataRow("((Param == A) && (Param == C))", "C++2", "A|B", false)]
        public void TestEvaluateMultichoice(string condition, string evaluator, string multichoiceValues, bool expectedResult)
        {
            string variableName = "myPredicate";
            EvaluateMacroConfig macroConfig = new(variableName, "bool", condition, evaluator);

            IVariableCollection variables = new VariableCollection
            {
                ["A"] = "A",
                ["B"] = "B",
                ["C"] = "C",
                ["Param"] = new MultiValueParameter(multichoiceValues.TokenizeMultiValueParameter())
            };

            EvaluateMacro macro = new();
            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);
            Assert.AreEqual(expectedResult, variables[variableName]);
        }

        [TestMethod]
        [Obsolete("IMacro.EvaluateConfig is obsolete")]
        public void ObsoleteEvaluateConfigTest()
        {
            string variableName = "myPredicate";
            string evaluator = "C++";
            EvaluateMacroConfig macroConfig = new(variableName, "bool", "(7 > 3)", evaluator);

            IVariableCollection variables = new VariableCollection();

            EvaluateMacro macro = new();
            macro.EvaluateConfig(_engineEnvironmentSettings, variables, macroConfig);
            Assert.IsTrue((bool)variables[variableName]!);
        }
    }
}
