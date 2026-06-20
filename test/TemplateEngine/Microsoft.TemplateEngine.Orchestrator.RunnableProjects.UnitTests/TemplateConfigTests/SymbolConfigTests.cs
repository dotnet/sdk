// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    [TestClass]
    public class SymbolConfigTests
    {
        private static JsonObject ArrayConfigForSymbolWithFormsButNotIdentity
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ArrayConfigWithNameSymbolAndValueFormsButNotIdentity
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ArrayConfigWithNameSymbolAndValueFormsWithIdentity
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalse
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrue
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ConfigWithObjectValueFormDefinitionAddIdentityFalse
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject NameConfigWithObjectValueFormDefinitionAddIdentityTrue
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject NameConfigWithObjectValueFormDefinitionAddIdentityFalse
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject NameConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject NameConfigObjectValueFormWithIdentityAndAddIdentityUnspecified
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ConfigForSymbolWithoutValueForms
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ArrayConfigForSymbolWithValueFormsIncludingIdentity
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ConfigWithObjectValueFormDefinitionAddIdentityTrue
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalse
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrue
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ParameterConfigObjectValueFormWithIdentityAndAddIdentityUnspecified
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        private static JsonObject ParameterConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified
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
                return JExtensions.ParseJsonObject(configString);
            }
        }

        // Test that when a config doesn't include a name parameter, one gets added - with the proper value forms.
        [TestMethod(DisplayName = nameof(NameSymbolGetsAddedWithDefaultValueForms))]
        public void NameSymbolGetsAddedWithDefaultValueForms()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigForSymbolWithFormsButNotIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();

            Assert.HasCount(5, configuredValueFormNames);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.AreEqual(DefaultSafeNameValueFormFactory.FormIdentifier, configuredValueFormNames[1]);
            Assert.AreEqual(DefaultLowerSafeNameValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.AreEqual(DefaultSafeNamespaceValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
            Assert.AreEqual(DefaultLowerSafeNamespaceValueFormFactory.FormIdentifier, configuredValueFormNames[4]);
        }

        // Test that when a symbol doesn't explicitly include the "identity" value form, it gets added as the first form.
        [TestMethod(DisplayName = nameof(ParameterSymbolWithoutIdentityValueFormGetsIdentityAddedAsFirst))]
        public void ParameterSymbolWithoutIdentityValueFormGetsIdentityAddedAsFirst()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigForSymbolWithFormsButNotIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            Assert.ContainsSingle(paramSymbol!.Forms.GlobalForms.ToList().Where(x => string.Equals(x, IdentityValueFormFactory.FormIdentifier, StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(0, paramSymbol.Forms.GlobalForms.ToList().IndexOf(IdentityValueFormFactory.FormIdentifier));
        }

        // Tests that a name symbol with explicitly defined value forms but no identity form
        // gets the identity form added as the first form.
        [TestMethod(DisplayName = nameof(ArrayConfigNameSymbolWithoutIdentityFormGetsIdentityFormAddedAsFirst))]
        public void ArrayConfigNameSymbolWithoutIdentityFormGetsIdentityFormAddedAsFirst()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigWithNameSymbolAndValueFormsButNotIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.AreEqual("foo", configuredValueFormNames[1]);
            Assert.AreEqual("bar", configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ArrayConfigNameSymbolWithIdentityFormRetainsConfiguredFormsExactly))]
        public void ArrayConfigNameSymbolWithIdentityFormRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigWithNameSymbolAndValueFormsWithIdentity);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(nameSymbol);
            IList<string> configuredValueFormNames = nameSymbol!.Forms.GlobalForms.ToList();
            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual("baz", configuredValueFormNames[2]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly))]
        public void ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityFalse);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(nameSymbol);
            var configuredValueFormNames = nameSymbol!.Forms.GlobalForms;
            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly))]
        public void ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigNameSymbolWithIdentityFormAndAddIdentityTrue);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(nameSymbol);
            var configuredValueFormNames = nameSymbol!.Forms.GlobalForms;
            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(NameSymbolObjectValueFormDefinitionRespectsAddIdentityTrue))]
        public void NameSymbolObjectValueFormDefinitionRespectsAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigWithObjectValueFormDefinitionAddIdentityTrue);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.AreEqual("foo", configuredValueFormNames[1]);
            Assert.AreEqual("bar", configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(NameSymbolObjectValueFormDefinitionRespectsAddIdentityFalse))]
        public void NameSymbolObjectValueFormDefinitionRespectsAddIdentityFalse()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigWithObjectValueFormDefinitionAddIdentityFalse);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.HasCount(3, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual("baz", configuredValueFormNames[2]);
        }

        [TestMethod(DisplayName = nameof(NameSymbolObjectValueFormDefinitionInfersAddIdentityTrue))]
        public void NameSymbolObjectValueFormDefinitionInfersAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.AreEqual("foo", configuredValueFormNames[1]);
            Assert.AreEqual("bar", configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(NameSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms))]
        public void NameSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(NameConfigObjectValueFormWithIdentityAndAddIdentityUnspecified);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "name");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual("baz", configuredValueFormNames[2]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ParameterSymbolWithNoValueFormsGetsIdentityFormAdded))]
        public void ParameterSymbolWithNoValueFormsGetsIdentityFormAdded()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ConfigForSymbolWithoutValueForms);

            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.ContainsSingle(configuredValueFormNames);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
        }

        // Test that when a symbol explicitly includes the "identity" value form, the value forms for the symbol remain unmodified.
        [TestMethod(DisplayName = nameof(ParameterSymbolWithArrayIdentityValueFormRetainsFormsUnmodified))]
        public void ParameterSymbolWithArrayIdentityValueFormRetainsFormsUnmodified()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ArrayConfigForSymbolWithValueFormsIncludingIdentity);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual("baz", configuredValueFormNames[2]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ObjectValueFormDefinitionRespectsAddIdentityTrue))]
        public void ObjectValueFormDefinitionRespectsAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ConfigWithObjectValueFormDefinitionAddIdentityTrue);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.AreEqual("foo", configuredValueFormNames[1]);
            Assert.AreEqual("bar", configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ObjectValueFormDefinitionRespectsAddIdentityFalse))]
        public void ObjectValueFormDefinitionRespectsAddIdentityFalse()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ConfigWithObjectValueFormDefinitionAddIdentityFalse);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            IList<string> configuredValueFormNames = paramSymbol!.Forms.GlobalForms.ToList();

            Assert.HasCount(3, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual("baz", configuredValueFormNames[2]);
        }

        [TestMethod(DisplayName = nameof(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly))]
        public void ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalseRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityFalse);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(nameSymbol);
            var configuredValueFormNames = nameSymbol!.Forms.GlobalForms;
            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly))]
        public void ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrueRetainsConfiguredFormsExactly()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ObjectConfigParameterSymbolWithIdentityFormAndAddIdentityTrue);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? nameSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(nameSymbol);
            var configuredValueFormNames = nameSymbol!.Forms.GlobalForms;
            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ParameterSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms))]
        public void ParameterSymbolObjectValueFormWithIdentityWithoutAddIdentityRetainsConfiguredForms()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ParameterConfigObjectValueFormWithIdentityAndAddIdentityUnspecified);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual("foo", configuredValueFormNames[0]);
            Assert.AreEqual("bar", configuredValueFormNames[1]);
            Assert.AreEqual("baz", configuredValueFormNames[2]);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[3]);
        }

        [TestMethod(DisplayName = nameof(ParameterSymbolObjectValueFormDefinitionInfersAddIdentityTrue))]
        public void ParameterSymbolObjectValueFormDefinitionInfersAddIdentityTrue()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(ParameterConfigObjectValueFormWithoutIdentityAndAddIdentityUnspecified);
            BaseSymbol symbolInfo = configModel.Symbols.Single(s => s.Name == "testSymbol");
            Assert.IsTrue(symbolInfo is ParameterSymbol);

            ParameterSymbol? paramSymbol = symbolInfo as ParameterSymbol;
            Assert.IsNotNull(paramSymbol);
            var configuredValueFormNames = paramSymbol!.Forms.GlobalForms;

            Assert.HasCount(4, configuredValueFormNames);
            Assert.AreEqual(IdentityValueFormFactory.FormIdentifier, configuredValueFormNames[0]);
            Assert.AreEqual("foo", configuredValueFormNames[1]);
            Assert.AreEqual("bar", configuredValueFormNames[2]);
            Assert.AreEqual("baz", configuredValueFormNames[3]);
        }

        [TestMethod]
        public void DefaultSymbolsaAreSetup()
        {
            TemplateConfigModel configModel = new TemplateConfigModel("test");
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            Assert.ContainsSingle(configModel.Symbols.Where(s => s.Name == "name"));
            if (isWindows)
            {
                Assert.HasCount(2, configModel.Symbols);
                Assert.ContainsSingle(configModel.Symbols.Where(s => s.Name == "OS"));
            }
            else
            {
                Assert.ContainsSingle(configModel.Symbols);
            }
        }

        [TestMethod]
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
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(templateConfig))!.AsObject());
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            Assert.ContainsSingle(configModel.Symbols.Where(s => s.Name == "name"));
            Assert.ContainsSingle(configModel.Symbols.Where(s => s.Name == "other"));
            if (isWindows)
            {
                Assert.HasCount(3, configModel.Symbols);
                Assert.ContainsSingle(configModel.Symbols.Where(s => s.Name == "OS"));
            }
            else
            {
                Assert.HasCount(2, configModel.Symbols);
            }
        }

        [TestMethod]
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
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(templateConfig))!.AsObject());
            Assert.HasCount(2, configModel.Symbols);
            Assert.ContainsSingle(configModel.Symbols.Where(s => s.Name == "name"));
            Assert.ContainsSingle(configModel.Symbols.Where(s => s.Name == "OS"));
            Assert.AreEqual("smth", (configModel.Symbols.Single(s => s.Name == "OS") as BindSymbol)?.Binding);
        }
    }
}
