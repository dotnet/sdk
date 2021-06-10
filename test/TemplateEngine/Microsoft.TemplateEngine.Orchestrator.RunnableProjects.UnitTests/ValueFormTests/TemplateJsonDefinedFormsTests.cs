// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.ValueFormTests
{
    public class TemplateJsonDefinedFormsTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public TemplateJsonDefinedFormsTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(UnknownFormNameOnParameterSymbolDoesNotThrow))]
        public void UnknownFormNameOnParameterSymbolDoesNotThrow()
        {
            string templateJson = @"
{
  ""name"": ""TestTemplate"",
  ""identity"": ""TestTemplate"",
  ""symbols"": {
    ""mySymbol"": {
      ""type"": ""parameter"",
      ""replaces"": ""whatever"",
      ""forms"": {
        ""global"": [ ""fakeName"" ],
      }
    }
  }
}
";
            JObject configObj = JObject.Parse(templateJson);
            SimpleConfigModel configModel = new SimpleConfigModel(_engineEnvironmentSettings, configObj);
            IGlobalRunConfig runConfig = null;

            try
            {
                runConfig = ((IRunnableProjectConfig)configModel).OperationConfig;
            }
            catch
            {
                Assert.True(false, "Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);
            Assert.Equal(1, runConfig.Macros.Count(m => m.VariableName.StartsWith("mySymbol")));
            var mySymbolMacro = runConfig.Macros.Single(m => m.VariableName.StartsWith("mySymbol"));

            Assert.True(mySymbolMacro is ProcessValueFormMacroConfig);
            ProcessValueFormMacroConfig identityFormConfig = mySymbolMacro as ProcessValueFormMacroConfig;
            Assert.Equal("identity", identityFormConfig.FormName);
        }

        [Fact(DisplayName = nameof(UnknownFormNameForDerivedSymbolValueDoesNotThrow))]
        public void UnknownFormNameForDerivedSymbolValueDoesNotThrow()
        {
            string templateJson = @"
{
  ""name"": ""TestTemplate"",
  ""identity"": ""TestTemplate"",
  ""symbols"": {
    ""original"": {
      ""type"": ""parameter"",
      ""replaces"": ""whatever"",
    },
    ""myDerivedSym"": {
      ""type"": ""derived"",
      ""valueSource"": ""original"",
      ""valueTransform"": ""fakeForm"",
      ""replaces"": ""something""
    }
  }
}
";
            JObject configObj = JObject.Parse(templateJson);
            SimpleConfigModel configModel = new SimpleConfigModel(_engineEnvironmentSettings, configObj);
            IGlobalRunConfig runConfig = null;

            try
            {
                runConfig = ((IRunnableProjectConfig)configModel).OperationConfig;
            }
            catch
            {
                Assert.True(false, "Should not throw on unknown value form name");
            }

            Assert.NotNull(runConfig);
        }
    }
}
