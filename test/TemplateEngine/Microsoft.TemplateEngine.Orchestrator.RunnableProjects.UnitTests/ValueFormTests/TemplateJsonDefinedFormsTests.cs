// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class TemplateJsonDefinedFormsTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public TemplateJsonDefinedFormsTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(UnknownFormNameOnParameterSymbolDoesNotThrow))]
        public void UnknownFormNameOnParameterSymbolDoesNotThrow()
        {
            string templateJson = /*lang=json*/ """
                {
                  "name": "TestTemplate",
                  "identity": "TestTemplate",
                  "symbols": {
                    "mySymbol": {
                      "type": "parameter",
                      "replaces": "whatever",
                      "forms": {
                        "global": [ "fakeName" ],
                      }
                    }
                  }
                }
                """;
            JObject configObj = JObject.Parse(templateJson);
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(configObj);
            IGlobalRunConfig? runConfig = null;

            try
            {
                runConfig = new RunnableProjectConfig(_engineEnvironmentSettings, A.Fake<IGenerator>(), configModel, A.Fake<IDirectory>()).GlobalOperationConfig;
            }
            catch
            {
                Assert.True(false, "Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);
            Assert.Equal(1, runConfig!.Macros.Count(m => m.VariableName.StartsWith("mySymbol")));
            var mySymbolMacro = runConfig.Macros.Single(m => m.VariableName.StartsWith("mySymbol"));

            Assert.True(mySymbolMacro is ProcessValueFormMacroConfig);
            ProcessValueFormMacroConfig? identityFormConfig = mySymbolMacro as ProcessValueFormMacroConfig;
            Assert.NotNull(identityFormConfig);
            Assert.Equal("identity", identityFormConfig!.FormName);
        }

        [Fact(DisplayName = nameof(UnknownFormNameForDerivedSymbolValueDoesNotThrow))]
        public void UnknownFormNameForDerivedSymbolValueDoesNotThrow()
        {
            string templateJson = /*lang=json*/ """
                {
                  "name": "TestTemplate",
                  "identity": "TestTemplate",
                  "symbols": {
                    "original": {
                      "type": "parameter",
                      "replaces": "whatever",
                    },
                    "myDerivedSym": {
                      "type": "derived",
                      "valueSource": "original",
                      "valueTransform": "fakeForm",
                      "replaces": "something"
                    }
                  }
                }
                """;
            JObject configObj = JObject.Parse(templateJson);
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(configObj);
            IGlobalRunConfig? runConfig = null;

            try
            {
                runConfig = new RunnableProjectConfig(_engineEnvironmentSettings, A.Fake<IGenerator>(), configModel, A.Fake<IDirectory>()).GlobalOperationConfig;
            }
            catch
            {
                Assert.True(false, "Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);
        }
    }
}
