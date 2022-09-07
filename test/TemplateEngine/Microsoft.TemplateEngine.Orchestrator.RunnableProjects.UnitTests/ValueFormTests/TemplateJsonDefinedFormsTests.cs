// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

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
                Symbols = new List<BaseSymbol>()
                {
                    new ParameterSymbol("mySymbol", "whatever")
                    {
                         Forms = new SymbolValueFormsModel(new[] { "identity", "fakeName" })
                    }
                }
            };

            GlobalRunConfig? runConfig = null;

            try
            {
                runConfig = new RunnableProjectConfig(environmentSettings, A.Fake<IGenerator>(), model, A.Fake<IDirectory>()).GlobalOperationConfig;
            }
            catch
            {
                Assert.True(false, "Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);
            Assert.Equal(1, runConfig.ComputedMacros.Count(m => m.VariableName.StartsWith("mySymbol")));
            BaseMacroConfig mySymbolMacro = runConfig.ComputedMacros.Single(m => m.VariableName.StartsWith("mySymbol"));

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
                Symbols = new List<BaseSymbol>()
                {
                    new ParameterSymbol("original", "whatever"),
                    new DerivedSymbol("myDerivedSym", valueTransform: "fakeForm", valueSource: "original", replaces: "something")
                }
            };

            GlobalRunConfig? runConfig = null;

            try
            {
                runConfig = new RunnableProjectConfig(environmentSettings, A.Fake<IGenerator>(), model, A.Fake<IDirectory>()).GlobalOperationConfig;
            }
            catch
            {
                Assert.True(false, "Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);

            Assert.Equal("The symbol 'myDerivedSym': unable to find a form 'fakeForm', the further processing of the symbol will be skipped.", loggedMessages.Single(m => m.Level == LogLevel.Warning).Message);
        }
    }
}
