using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class SymbolConfigTests : TestBase
    {
        // Test that when a config doesn't include a name parameter, one gets added - with the proper value forms.
        [Fact(DisplayName = nameof(NameSymbolGetsAddedWithDefaultValueForms))]
        public void NameSymbolGetsAddedWithDefaultValueForms()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, ConfigForSymbolWithFormsButNotIdentity);
            Assert.True(configModel.Symbols.ContainsKey("name"));

            ISymbolModel symbolInfo = configModel.Symbols["name"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol nameSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = nameSymbol.Forms.GlobalForms.ToList();

            Assert.Equal(5, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueForm.FormName, configuredValueFormNames[0]);
            Assert.Equal(DefaultSafeNameValueFormModel.FormName, configuredValueFormNames[1]);
            Assert.Equal(DefaultLowerSafeNameValueFormModel.FormName, configuredValueFormNames[2]);
            Assert.Equal(DefaultSafeNamespaceValueFormModel.FormName, configuredValueFormNames[3]);
            Assert.Equal(DefaultLowerSafeNamespaceValueFormModel.FormName, configuredValueFormNames[4]);
        }

        // Test that when a symbol doens't explicitly include the "identity" value form, it gets added as the first form.
        [Fact(DisplayName = nameof(ParameterSymbolWithoutIdentityValueFormGetsIdentityAddedAsFirst))]
        public void ParameterSymbolWithoutIdentityValueFormGetsIdentityAddedAsFirst()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, ConfigForSymbolWithFormsButNotIdentity);
            Assert.True(configModel.Symbols.ContainsKey("testSymbol"));

            ISymbolModel symbolInfo = configModel.Symbols["testSymbol"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol paramSymbol = symbolInfo as ParameterSymbol;
            Assert.Equal(1, paramSymbol.Forms.GlobalForms.ToList()
                                                .Where(x => string.Equals(x, IdentityValueForm.FormName, StringComparison.OrdinalIgnoreCase))
                                                .Count());
            Assert.Equal(0, paramSymbol.Forms.GlobalForms.ToList().IndexOf(IdentityValueForm.FormName));
        }

        private static JObject ConfigForSymbolWithFormsButNotIdentity
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""testSymbol"": {
      ""type"": ""parameter"",
      ""dataType"": ""string"",
      ""forms"": {
        ""global"": [ ""foo"", ""bar"", ""baz"" ]
      }
    }
  }
}";
                return JObject.Parse(configString);
            }
        }

        // Tests that a name symbol with explicitly defined value forms but no identity form
        // gets the identity form added as the first form.
        [Fact(DisplayName = nameof(ConfigDefinedNameSymbolWithoutIdentityFormGetsIdentityFormAddedAsFirst))]
        public void ConfigDefinedNameSymbolWithoutIdentityFormGetsIdentityFormAddedAsFirst()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, ConfigWithNameSymbolAndValueFormsButNotIdentity);
            Assert.True(configModel.Symbols.ContainsKey("name"));

            ISymbolModel symbolInfo = configModel.Symbols["name"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol nameSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = nameSymbol.Forms.GlobalForms.ToList();
            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueForm.FormName, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        private static JObject ConfigWithNameSymbolAndValueFormsButNotIdentity
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""name"": {
      ""type"": ""parameter"",
      ""dataType"": ""string"",
      ""forms"": {
        ""global"": [ ""foo"", ""bar"", ""baz"" ]
      }
    }
  }
}";
                return JObject.Parse(configString);
            }
        }

        // Test that when a symbol explicitly includes the "identity" value form, the value forms for the symbol remain unmodified.
        [Fact(DisplayName = nameof(ParameterSymbolWithIdentityValueFormRetainsFormsUnmodified))]
        public void ParameterSymbolWithIdentityValueFormRetainsFormsUnmodified()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, ConfigForSymbolWithValueFormsIncludingIdentity);
            Assert.True(configModel.Symbols.ContainsKey("testSymbol"));

            ISymbolModel symbolInfo = configModel.Symbols["testSymbol"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol paramSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = paramSymbol.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
            Assert.Equal(IdentityValueForm.FormName, configuredValueFormNames[3]);
        }

        private static JObject ConfigForSymbolWithValueFormsIncludingIdentity
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""testSymbol"": {
      ""type"": ""parameter"",
      ""dataType"": ""string"",
      ""forms"": {
        ""global"": [ ""foo"", ""bar"", ""baz"", ""identity"" ]
      }
    }
  }
}";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(ParameterSymbolWithNoValueFormsGetsIdentityFormAdded))]
        public void ParameterSymbolWithNoValueFormsGetsIdentityFormAdded()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, ConfigForSymbolWithoutValueForms);
            Assert.True(configModel.Symbols.ContainsKey("testSymbol"));

            ISymbolModel symbolInfo = configModel.Symbols["testSymbol"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol paramSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = paramSymbol.Forms.GlobalForms.ToList();

            Assert.Equal(1, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueForm.FormName, configuredValueFormNames[0]);
        }

        private static JObject ConfigForSymbolWithoutValueForms
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""testSymbol"": {
      ""type"": ""parameter"",
      ""dataType"": ""string""
    }
  }
}";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(ObjectValueFormDefinitionRespectsAddIdentityTrue))]
        public void ObjectValueFormDefinitionRespectsAddIdentityTrue()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, ConfigWithObjectValueFormDefinitionAddIdentityTrue);
            Assert.True(configModel.Symbols.ContainsKey("testSymbol"));

            ISymbolModel symbolInfo = configModel.Symbols["testSymbol"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol paramSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = paramSymbol.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueForm.FormName, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        private static JObject ConfigWithObjectValueFormDefinitionAddIdentityTrue
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""testSymbol"": {
      ""type"": ""parameter"",
      ""dataType"": ""string"",
      ""forms"": {
        ""global"": {
            ""forms"": [ ""foo"", ""bar"", ""baz"" ],
            ""addIdentity"": ""true""
        }
      }
    }
  }
}";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(ObjectValueFormDefinitionRespectsAddIdentityFalse))]
        public void ObjectValueFormDefinitionRespectsAddIdentityFalse()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, ConfigWithObjectValueFormDefinitionAddIdentityFalse);
            Assert.True(configModel.Symbols.ContainsKey("testSymbol"));

            ISymbolModel symbolInfo = configModel.Symbols["testSymbol"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol paramSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = paramSymbol.Forms.GlobalForms.ToList();

            Assert.Equal(3, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
        }

        private static JObject ConfigWithObjectValueFormDefinitionAddIdentityFalse
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""testSymbol"": {
      ""type"": ""parameter"",
      ""dataType"": ""string"",
      ""forms"": {
        ""global"": {
            ""forms"": [ ""foo"", ""bar"", ""baz"" ],
            ""addIdentity"": ""false""
        }
      }
    }
  }
}";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(NameSymbolObjectValueFormDefinitionRespectsAddIdentityTrue))]
        public void NameSymbolObjectValueFormDefinitionRespectsAddIdentityTrue()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, NameConfigWithObjectValueFormDefinitionAddIdentityTrue);
            Assert.True(configModel.Symbols.ContainsKey("testSymbol"));

            ISymbolModel symbolInfo = configModel.Symbols["testSymbol"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol paramSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = paramSymbol.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueForm.FormName, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        private static JObject NameConfigWithObjectValueFormDefinitionAddIdentityTrue
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""testSymbol"": {
      ""type"": ""parameter"",
      ""dataType"": ""string"",
      ""forms"": {
        ""global"": {
            ""forms"": [ ""foo"", ""bar"", ""baz"" ],
            ""addIdentity"": ""true""
        }
      }
    }
  }
}";
                return JObject.Parse(configString);
            }
        }

        [Fact(DisplayName = nameof(NameSymbolObjectValueFormDefinitionRespectsAddIdentityFalse))]
        public void NameSymbolObjectValueFormDefinitionRespectsAddIdentityFalse()
        {
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(EngineEnvironmentSettings, NameConfigWithObjectValueFormDefinitionAddIdentityFalse);
            Assert.True(configModel.Symbols.ContainsKey("testSymbol"));

            ISymbolModel symbolInfo = configModel.Symbols["testSymbol"];
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol paramSymbol = symbolInfo as ParameterSymbol;
            IList<string> configuredValueFormNames = paramSymbol.Forms.GlobalForms.ToList();

            Assert.Equal(3, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
        }

        private static JObject NameConfigWithObjectValueFormDefinitionAddIdentityFalse
        {
            get
            {
                string configString = @"
{
  ""author"": ""Test Asset"",
  ""classifications"": [ ""Test Asset"" ],
  ""name"": ""TemplateWithValueForms"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""TestAssets.TemplateWithValueForms"",
  ""precedence"": ""100"",
  ""identity"": ""TestAssets.TemplateWithValueForms"",
  ""shortName"": ""TestAssets.TemplateWithValueForms"",
  ""symbols"": {
    ""testSymbol"": {
      ""type"": ""parameter"",
      ""dataType"": ""string"",
      ""forms"": {
        ""global"": {
            ""forms"": [ ""foo"", ""bar"", ""baz"" ],
            ""addIdentity"": ""false""
        }
      }
    }
  }
}";
                return JObject.Parse(configString);
            }
        }
    }
}
