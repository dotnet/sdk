// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class SymbolConfigTests
    {
        private static JObject ArrayConfigForSymbolWithFormsButNotIdentity
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": [ "foo", "bar", "baz" ]
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ArrayConfigWithNameSymbolAndValueFormsButNotIdentity
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": [ "foo", "bar", "baz" ]
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ConfigWithNameSymbolWithoutBinding
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithNameSymbolWithoutBinding",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithNameSymbolWithoutBinding",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithNameSymbolWithoutBinding",
                  "shortName": "TestAssets.TemplateWithNameSymbolWithoutBinding",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ConfigWithNameSymbolWithCustomBinding
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "ConfigWithNameSymbolWithCustomBinding",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.ConfigWithNameSymbolWithCustomBinding",
                  "precedence": "100",
                  "identity": "TestAssets.ConfigWithNameSymbolWithCustomBinding",
                  "shortName": "TestAssets.ConfigWithNameSymbolWithCustomBinding",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "binding": "customBinding",
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ArrayConfigWithNameSymbolAndValueFormsWithIdentity
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": [ "foo", "bar", "baz", "identity" ]
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalse
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "identity", "baz" ],
                            "addIdentity": "false"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrue
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "identity", "baz" ],
                            "addIdentity": "true"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ConfigWithObjectValueFormDefinitionAddIdentityFalse
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz" ],
                            "addIdentity": "false"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject NameConfigWithObjectValueFormDefinitionAddIdentityTrue
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz" ],
                            "addIdentity": "true"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject NameConfigWithObjectValueFormDefinitionAddIdentityFalse
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz" ],
                            "addIdentity": "false"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject NameConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz" ],
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject NameConfigObjectValueFormWithIdentityAndAddIdentityUnspecified
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "name": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz", "identity" ],
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ConfigForSymbolWithoutValueForms
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string"
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ArrayConfigForSymbolWithValueFormsIncludingIdentity
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": [ "foo", "bar", "baz", "identity" ]
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ConfigWithObjectValueFormDefinitionAddIdentityTrue
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz" ],
                            "addIdentity": "true"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalse
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "identity", "baz" ],
                            "addIdentity": "false"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrue
        {
            get
            {
                string configString = /*lang=json,strict*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "identity", "baz" ],
                            "addIdentity": "true"
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ParameterConfigObjectValueFormWithIdentityAndAddIdentityUnspecified
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz", "identity" ],
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        private static JObject ParameterConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithValueForms",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "TestAssets.TemplateWithValueForms",
                  "precedence": "100",
                  "identity": "TestAssets.TemplateWithValueForms",
                  "shortName": "TestAssets.TemplateWithValueForms",
                  "symbols": {
                    "testSymbol": {
                      "type": "parameter",
                      "dataType": "string",
                      "forms": {
                        "global": {
                            "forms": [ "foo", "bar", "baz" ],
                        }
                      }
                    }
                  }
                }
                """;
                return JObject.Parse(configString);
            }
        }

        // Test that when a config doesn't include a name parameter, one gets added - with the proper value forms.
        [Fact(DisplayName = nameof(NameSymbolGetsAddedWithDefaultValueForms))]
        public void NameSymbolGetsAddedWithDefaultValueForms()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigForSymbolWithFormsButNotIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(5, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.Equal(DefaultSafeNameValueFormFactory.FormIdentifier, configuredValueFormNames[1]);
            Assert.Equal(DefaultLowerSafeNameValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.Equal(DefaultSafeNamespaceValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
            Assert.Equal(DefaultLowerSafeNamespaceValueFormFactory.FormIdentifier, configuredValueFormNames[4]);
        }

        // Test that when a symbol doesn't explicitly include the "identity" value form, it gets added as the first form.
        [Fact(DisplayName = nameof(ParameterSymbolWithoutIdentityValueFormGetsIdentityAddedAsFirst))]
        public void ParameterSymbolWithoutIdentityValueFormGetsIdentityAddedAsFirst()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigForSymbolWithFormsButNotIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            Assert.Single(paramSymbol!.Forms.GlobalForms.ToList()
                                                .Where(x => string.Equals(x, IdentityValueFormFactory.FormIdentifier, StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(0, paramSymbol.Forms.GlobalForms.ToList().IndexOf(IdentityValueFormFactory.FormIdentifier));
        }

        // Tests that a name symbol with explicitly defined value forms but no identity form
        // gets the identity form added as the first form.
        [Fact(DisplayName = nameof(ArrayConfigNameSymbolWithoutIdentityFormGetsIdentityFormAddedAsFirst))]
        public void ArrayConfigNameSymbolWithoutIdentityFormGetsIdentityFormAddedAsFirst()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigWithNameSymbolAndValueFormsButNotIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ArrayConfigNameSymbolWithIdentityFormRetainsConfiguredFormsExactly))]
        public void ArrayConfigNameSymbolWithIdentityFormRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigWithNameSymbolAndValueFormsWithIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly))]
        public void ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalse);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly))]
        public void ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrue);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(NameSymbolObjectValueFormDefinitionRespectsAddIdentityTrue))]
        public void NameSymbolObjectValueFormDefinitionRespectsAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigWithObjectValueFormDefinitionAddIdentityTrue);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(NameSymbolObjectValueFormDefinitionRespectsAddIdentityFalse))]
        public void NameSymbolObjectValueFormDefinitionRespectsAddIdentityFalse()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigWithObjectValueFormDefinitionAddIdentityFalse);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(3, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
        }

        [Fact(DisplayName = nameof(NameSymbolObjectValueFormDefinitionInfersAddIdentityTrue))]
        public void NameSymbolObjectValueFormDefinitionInfersAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(NameSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms))]
        public void NameSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigObjectValueFormWithIdentityAndAddIdentityUnspecified);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ParameterSymbolWithNoValueFormsGetsIdentityFormAdded))]
        public void ParameterSymbolWithNoValueFormsGetsIdentityFormAdded()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ConfigForSymbolWithoutValueForms);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Single(configuredValueFormNames);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
        }

        // Test that when a symbol explicitly includes the "identity" value form, the value forms for the symbol remain unmodified.
        [Fact(DisplayName = nameof(ParameterSymbolWithArrayIdentityValueFormRetainsFormsUnmodified))]
        public void ParameterSymbolWithArrayIdentityValueFormRetainsFormsUnmodified()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigForSymbolWithValueFormsIncludingIdentity);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ObjectValueFormDefinitionRespectsAddIdentityTrue))]
        public void ObjectValueFormDefinitionRespectsAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ConfigWithObjectValueFormDefinitionAddIdentityTrue);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ObjectValueFormDefinitionRespectsAddIdentityFalse))]
        public void ObjectValueFormDefinitionRespectsAddIdentityFalse()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ConfigWithObjectValueFormDefinitionAddIdentityFalse);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(3, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
        }

        [Fact(DisplayName = nameof(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly))]
        public void ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalse);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly))]
        public void ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrue);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ParameterSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms))]
        public void ParameterSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ParameterConfigObjectValueFormWithIdentityAndAddIdentityUnspecified);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal("foo", configuredValueFormNames[0]);
            Assert.Equal("bar", configuredValueFormNames[1]);
            Assert.Equal("baz", configuredValueFormNames[2]);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [Fact(DisplayName = nameof(ParameterSymbolObjectValueFormDefinitionInfersAddIdentityTrue))]
        public void ParameterSymbolObjectValueFormDefinitionInfersAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ParameterConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.True(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.NotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.Equal(4, configuredValueFormNames.Count);
            Assert.Equal(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.Equal("foo", configuredValueFormNames[1]);
            Assert.Equal("bar", configuredValueFormNames[2]);
            Assert.Equal("baz", configuredValueFormNames[3]);
        }

        [Fact]
        public void DefaultSymbolsaAreSetup()
        {
            TemplateConfigModel configModel = new TemplateConfigModel("test");
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            Assert.Single(configModel.Symbols, s => s.Name == "name");
            if (isWindows)
            {
                Assert.Equal(2, configModel.Symbols.Count());
                Assert.Single(configModel.Symbols, s => s.Name == "OS");
            }
            else
            {
                Assert.Single(configModel.Symbols);
            }
        }

        [Fact]
        public void DefaultSymbolsaAreSetup_OnReadingFromJson()
        {
            var templateConfig = new
            {
                identity = "test.template",
                symbols = new
                {
                    other = new
                    {
                        type = "bind",
                        binding = "host:HostIdentifier",
                    },
                }
            };
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            Assert.Single(configModel.Symbols, s => s.Name == "name");
            Assert.Single(configModel.Symbols, s => s.Name == "other");
            if (isWindows)
            {
                Assert.Equal(3, configModel.Symbols.Count());
                Assert.Single(configModel.Symbols, s => s.Name == "OS");
            }
            else
            {
                Assert.Equal(2, configModel.Symbols.Count());
            }
        }

        [Fact]
        public void DefaultSymbolsaAreSetup_ImplicitBindWillNotOverwrite()
        {
            var templateConfig = new
            {
                identity = "test.template",
                symbols = new
                {
                    OS = new
                    {
                        type = "bind",
                        binding = "smth",
                    },
                }
            };
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            Assert.Equal(2, configModel.Symbols.Count());
            Assert.Single(configModel.Symbols, s => s.Name == "name");
            Assert.Single(configModel.Symbols, s => s.Name == "OS");
            Assert.Equal("smth", (configModel.Symbols.Single(s => s.Name == "OS") as BindSymbol)?.Binding);
        }
    }
}
