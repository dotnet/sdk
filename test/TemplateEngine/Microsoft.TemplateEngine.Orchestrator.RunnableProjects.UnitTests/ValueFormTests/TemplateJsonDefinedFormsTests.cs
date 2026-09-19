// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    [TestClass]
    public class TemplateJsonDefinedFormsTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
        public void UnknownFormNameOnParameterSymbolDoesNotThrow()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            TemplateConfigModel model = new("TestTemplate")
            {
                Name = "TestTemplate",
                ShortNameList = new[] { "TestTemplate" },
                Symbols = new List<BaseSymbol>()
                {
                    new ParameterSymbol("mySymbol", "whatever")
                    {
                         Forms = new SymbolValueFormsModel(new[] { "identity", "fakeName" })
                    }
                }
            };
            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            using RunnableProjectConfig runConfig = new RunnableProjectConfig(environmentSettings, new RunnableProjectGenerator(), model, mountPoint.Root);
            GlobalRunConfig? globalRunConfig = runConfig.GlobalOperationConfig;

            Assert.IsNotNull(runConfig);
            Assert.ContainsSingle(globalRunConfig.Macros.Where(m => m.VariableName.StartsWith("mySymbol")));
            IMacroConfig mySymbolMacro = globalRunConfig.Macros.Single(m => m.VariableName.StartsWith("mySymbol"));

            Assert.IsTrue(mySymbolMacro is ProcessValueFormMacroConfig);
            ProcessValueFormMacroConfig? identityFormConfig = mySymbolMacro as ProcessValueFormMacroConfig;
            Assert.IsNotNull(identityFormConfig);
            Assert.AreEqual("identity", identityFormConfig.Form.Identifier);

            Assert.AreEqual("The symbol 'mySymbol': unable to find a form 'fakeName', the further processing of the symbol will be skipped.", loggedMessages.Single(m => m.Level == LogLevel.Warning).Message);
        }

        [TestMethod]
        public void UnknownFormNameForDerivedSymbolValueDoesThrow()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            TemplateConfigModel model = new("TestTemplate")
            {
                Name = "TestTemplate",
                ShortNameList = new[] { "TestTemplate" },
                Symbols = new List<BaseSymbol>()
                {
                    new ParameterSymbol("original", "whatever"),
                    new DerivedSymbol("myDerivedSym", valueTransform: "fakeForm", valueSource: "original", replaces: "something")
                }
            };

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            using RunnableProjectConfig runConfig = new RunnableProjectConfig(environmentSettings, new RunnableProjectGenerator(), model, mountPoint.Root);
            GlobalRunConfig? globalRunConfig = runConfig.GlobalOperationConfig;

            Assert.IsNotNull(runConfig);

            Assert.AreEqual("The symbol 'myDerivedSym': unable to find a form 'fakeForm', the further processing of the symbol will be skipped.", loggedMessages.Single(m => m.Level == LogLevel.Warning).Message);
        }
    }
}
