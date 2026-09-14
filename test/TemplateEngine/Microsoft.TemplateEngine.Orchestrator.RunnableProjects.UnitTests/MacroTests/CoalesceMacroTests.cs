// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    [TestClass]
    public class CoalesceMacroTests
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        public CoalesceMacroTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [TestMethod]
        [DataRow(null, null, null, null)]
        [DataRow("", "", null, "")]
        [DataRow(null, "fallback", null, "fallback")]
        [DataRow("", "fallback", null, "fallback")]
        [DataRow("def", "fallback", "def", "fallback")]
        [DataRow("def", "fallback", "", "def")]
        public void CoalesceMacroTest(string? sourceValue, string? fallbackValue, string? defaultValue, string? expectedResult)
        {
            CoalesceMacro macro = new();
            CoalesceMacroConfig macroConfig = new(macro, "test", "string", "varA", defaultValue, "varB");

            VariableCollection variables = new();
            if (sourceValue != null)
            {
                variables["varA"] = sourceValue;
            }
            if (fallbackValue != null)
            {
                variables["varB"] = fallbackValue;
            }

            macro.Evaluate(_engineEnvironmentSettings, variables, macroConfig);

            if (expectedResult == null)
            {
                Assert.IsFalse(variables.ContainsKey("test"));
            }
            else
            {
                Assert.AreEqual(expectedResult, variables["test"]);
            }
        }

        [TestMethod]
        [DataRow(null, null, null, null)]
        [DataRow("", "", null, "")]
        [DataRow(null, "fallback", null, "fallback")]
        [DataRow("", "fallback", null, "fallback")]
        [DataRow("def", "fallback", "def", "fallback")]
        [DataRow("def", "fallback", "", "def")]
        public void GeneratedSymbolTest(string? sourceValue, string? fallbackValue, string? defaultValue, string? expectedResult)
        {
            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "sourceVariableName", JExtensions.ToJsonString("varA") },
                { "fallbackVariableName", JExtensions.ToJsonString("varB") }
            };
            if (defaultValue != null)
            {
                jsonParameters["defaultValue"] = JExtensions.ToJsonString(defaultValue);
            }

            VariableCollection variables = new();
            if (sourceValue != null)
            {
                variables["varA"] = sourceValue;
            }
            if (fallbackValue != null)
            {
                variables["varB"] = fallbackValue;
            }

            macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters));

            if (expectedResult == null)
            {
                Assert.IsFalse(variables.ContainsKey("test"));
            }
            else
            {
                Assert.AreEqual(expectedResult, variables["test"]);
            }
        }

        [TestMethod]
        public void GeneratedSymbolTest_DefaultValueLeadsToFallback()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "sourceVariableName", JExtensions.ToJsonString("varA") },
                { "fallbackVariableName", JExtensions.ToJsonString("varB") },
                { "defaultValue", JExtensions.ToJsonString("0") }
            };

            VariableCollection variables = new()
            {
                ["varA"] = 0,
                ["varB"] = 10
            };

            macro.Evaluate(environmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters));
            Assert.AreEqual(10, variables["test"]);
            Assert.AreEqual("[CoalesceMacro]: 'test': source value '0' is not used, because it is equal to default value '0'.", loggedMessages.First().Message);
        }

        [TestMethod]
        public void GeneratedSymbolTest_ExplicitDefaultValuesArePreserved()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "sourceVariableName", JExtensions.ToJsonString("varA") },
                { "fallbackVariableName", JExtensions.ToJsonString("varB") }
            };

            VariableCollection variables = new()
            {
                ["varA"] = 0,
                ["varB"] = 10
            };

            macro.Evaluate(environmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters));
            Assert.AreEqual(0, variables["test"]);
        }

        [TestMethod]
        public void InvalidConfigurationTest_Source()
        {
            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "fallbackVariableName", JExtensions.ToJsonString("varB") }
            };
            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.ThrowsExactly<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters)));
            Assert.AreEqual("Generated symbol 'test' of type 'coalesce' should have 'sourceVariableName' property defined.", ex.Message);
        }

        [TestMethod]
        public void InvalidConfigurationTest_Fallback()
        {
            CoalesceMacro macro = new();

            Dictionary<string, string> jsonParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                { "sourceVariableName", JExtensions.ToJsonString("varA") }
            };
            VariableCollection variables = new();
            TemplateAuthoringException ex = Assert.ThrowsExactly<TemplateAuthoringException>(() => macro.Evaluate(_engineEnvironmentSettings, variables, new GeneratedSymbol("test", "coalesce", jsonParameters)));
            Assert.AreEqual("Generated symbol 'test' of type 'coalesce' should have 'fallbackVariableName' property defined.", ex.Message);
        }
    }
}
