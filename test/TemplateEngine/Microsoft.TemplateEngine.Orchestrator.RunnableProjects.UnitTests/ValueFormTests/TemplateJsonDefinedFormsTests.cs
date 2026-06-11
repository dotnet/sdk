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
    public class TemplateJsonDefinedFormsTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplateJsonDefinedFormsTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public void UnknownFormNameOnParameterSymbolDoesNotThrow()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

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

            GlobalRunConfig? globalRunConfig = null;
            try
            {
                globalRunConfig = runConfig.GlobalOperationConfig;
            }
            catch
            {
                Assert.Fail("Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);
            Assert.Equal(1, globalRunConfig.Macros.Count(m => m.VariableName.StartsWith("mySymbol")));
            IMacroConfig mySymbolMacro = globalRunConfig.Macros.Single(m => m.VariableName.StartsWith("mySymbol"));

            Assert.True(mySymbolMacro is ProcessValueFormMacroConfig);
            ProcessValueFormMacroConfig? identityFormConfig = mySymbolMacro as ProcessValueFormMacroConfig;
            Assert.NotNull(identityFormConfig);
            Assert.Equal("identity", identityFormConfig.Form.Identifier);

            Assert.Equal("The symbol 'mySymbol': unable to find a form 'fakeName', the further processing of the symbol will be skipped.", loggedMessages.Single(m => m.Level == LogLevel.Warning).Message);
        }

        [Fact]
        public void UnknownFormNameForDerivedSymbolValueDoesThrow()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

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

            GlobalRunConfig? globalRunConfig = null;
            try
            {
                globalRunConfig = runConfig.GlobalOperationConfig;
            }
            catch
            {
                Assert.Fail("Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);

            Assert.Equal("The symbol 'myDerivedSym': unable to find a form 'fakeForm', the further processing of the symbol will be skipped.", loggedMessages.Single(m => m.Level == LogLevel.Warning).Message);
        }
    }
}
