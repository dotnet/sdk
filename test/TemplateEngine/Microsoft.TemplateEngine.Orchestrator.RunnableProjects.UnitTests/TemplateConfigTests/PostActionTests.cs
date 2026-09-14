// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    [TestClass]
    public class PostActionTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _environmentSettings;

        public PostActionTests()
        {
            _environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
        }

        private static JsonObject TestTemplateJson
        {
            get
            {
                string configString = /*lang=json*/ """
                {
                  "Author": "Microsoft",
                  "Classifications": "UnitTest",
                  "name": "Dotnet Unit Test Template",
                  "groupIdentity": "Dotnet.Testing",
                  "identity": "Dotnet.UnitTest.Template",
                  "shortName": "test",
                  "PostActions": [
                    {
                      "Condition": "(ActionOneCondition)",
                      "Description": "Action1",
                      "ActionId": "7F0CDCFC-839A-4625-88F6-27590E6299EF",
                      "ContinueOnError": "false",
                      "Args": {
                        "Foo": "Bar",
                        "Baz": "Blah"
                      },
                      "ManualInstructions": [
                        {
                          "Text": "Windows instructions (action 1)",
                          "condition": "(OperatingSystemKind == \"Windows\")"
                        },
                        {
                          "Text": "Linux instructions (action 1)",
                          "condition": "(OperatingSystemKind == \"Linux\")"
                        },
                        {
                          "Text": "Mac instructions (action 1)",
                          "condition": "(OperatingSystemKind == \"Mac\")"
                        },
                        {
                          "Text": "Default instructions (action 1)"
                        }
                      ]
                    },
                    {
                      "Condition": "(ActionTwoCondition)",
                      "Description": "Action2",
                      "ActionId": "95858732-757E-42AD-8D63-8A4D8C35B829",
                      "ContinueOnError": "false",
                      "Args": {
                        "Bar": "Foo",
                        "Blah": "Baz",
                        "X": "Y"
                      },
                      "ManualInstructions": [
                        {
                          "Text": "Mac instructions (action 2)",
                          "condition": "(OperatingSystemKind == \"Mac\")"
                        },
                        {
                          "Text": "Linux instructions (action 2)",
                          "condition": "(OperatingSystemKind == \"Linux\")"
                        },
                        {
                          "Text": "Windows instructions (action 2)",
                          "condition": "(OperatingSystemKind == \"Windows\")"
                        },
                        {
                          "Text": "Default instructions (action 2)"
                        }
                      ]
                    },
                    {
                      "Condition": "(ActionThreeCondition)",
                      "Description": "Action3",
                      "ActionId": "A7D9715C-7582-413E-A3FC-4C25D07D2A33",
                      "ContinueOnError": "false",
                      "Args": {
                        "Foo": "Bang",
                        "Baz": "Blah"
                      },
                      "ManualInstructions": [
                        {
                          "Text": "First instruction (action 3)"
                        },
                        {
                          "Text": "Second instruction (action 3)"
                        },
                        {
                          "Text": "Third instruction (action 3)",
                        }
                      ]
                    },
                    {
                      "Condition": "(ActionFourCondition)",
                      "Description": "Action4",
                      "ActionId": "9B4C660F-2834-4C98-9DCE-40123C38BD7B",
                      "ContinueOnError": "false",
                      "Args": {
                        "Foo": "Bang",
                        "Baz": "Blah"
                      },
                      "ManualInstructions": [
                        {
                          "Text": "Windows instructions (action 4)",
                          "condition": "(OperatingSystemKind == \"Windows\")"
                        },
                        {
                          "Text": "Windows-NET instructions (action 4)",
                          "condition": "(OperatingSystemKind == \"Windows\" && Framework == \"NET\")"
                        },
                        {
                          "Text": "Windows-NET-C# instructions (action 4)",
                          "condition": "(OperatingSystemKind == \"Windows\" && Framework == \"NET\" && Language == \"C#\")"
                        },
                        {
                          "Text": "Default instructions (action 4)"
                        }
                      ]
                    }
                  ]
                }
                """;
                return JExtensions.ParseJsonObject(configString);
            }
        }

        [TestMethod]
        [DataRow(true, true, 2, new[] { "Action1", "Default instructions (action 1)" }, new[] { "Action2", "Default instructions (action 2)" })]
        [DataRow(true, false, 1, new[] { "Action1", "Default instructions (action 1)" }, null)]
        [DataRow(false, true, 1, new[] { "Action2", "Default instructions (action 2)" }, null)]
        [DataRow(false, false, 0, null, null)]
        public void TestPostActionConditioning(bool condition1, bool condition2, int expectedActionCount, string[]? firstResult, string[]? secondResult)
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(TestTemplateJson);
            IVariableCollection vc = new VariableCollection
            {
                ["ActionOneCondition"] = condition1,
                ["ActionTwoCondition"] = condition2
            };

            FileRenameGenerator renameGenerator = new(
                _environmentSettings,
                "sourceName",
                "MyProject",
                vc,
                []);

            List<IPostAction> postActions = PostAction.Evaluate(
                _environmentSettings,
                configModel.PostActionModels,
                vc,
                renameGenerator);

            Assert.HasCount(expectedActionCount, postActions);
            if (firstResult != null && firstResult.Length > 0)
            {
                Assert.AreEqual(firstResult[0], postActions[0].Description);
                Assert.AreEqual(firstResult[1], postActions[0].ManualInstructions);
            }

            if (secondResult != null && secondResult.Length > 0)
            {
                Assert.AreEqual(secondResult[0], postActions[1].Description);
                Assert.AreEqual(secondResult[1], postActions[1].ManualInstructions);
            }
        }

        [TestMethod]
        [DataRow(true, true, 2, "Windows", "Windows instructions (action 1)", "Windows instructions (action 2)")]
        [DataRow(true, true, 2, "Linux", "Linux instructions (action 1)", "Linux instructions (action 2)")]
        [DataRow(true, true, 2, "Mac", "Mac instructions (action 1)", "Mac instructions (action 2)")]
        [DataRow(true, true, 2, "BeOS", "Default instructions (action 1)", "Default instructions (action 2)")]
        [DataRow(true, false, 1, "Windows", "Windows instructions (action 1)", null)]
        [DataRow(true, false, 1, "Linux", "Linux instructions (action 1)", null)]
        [DataRow(true, false, 1, "Mac", "Mac instructions (action 1)", null)]
        [DataRow(true, false, 1, "BeOS", "Default instructions (action 1)", null)]
        [DataRow(false, true, 1, "Windows", "Windows instructions (action 2)", null)]
        [DataRow(false, true, 1, "Linux", "Linux instructions (action 2)", null)]
        [DataRow(false, true, 1, "Mac", "Mac instructions (action 2)", null)]
        [DataRow(false, true, 1, "BeOS", "Default instructions (action 2)", null)]
        public void TestPostActionInstructionsConditioning(bool condition1, bool condition2, int expectedActionCount, string operatingSystemValue, string firstInstruction, string? secondInstruction)
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(TestTemplateJson);
            IVariableCollection vc = new VariableCollection
            {
                ["ActionOneCondition"] = condition1,
                ["ActionTwoCondition"] = condition2,
                ["OperatingSystemKind"] = operatingSystemValue
            };

            FileRenameGenerator renameGenerator = new(
                    _environmentSettings,
                    "sourceName",
                    "MyProject",
                    vc,
                    []);

            List<IPostAction> postActions = PostAction.Evaluate(
                _environmentSettings,
                configModel.PostActionModels,
                vc,
                renameGenerator);

            Assert.HasCount(expectedActionCount, postActions);

            if (!string.IsNullOrEmpty(firstInstruction))
            {
                Assert.AreEqual(firstInstruction, postActions[0].ManualInstructions);
            }

            if (!string.IsNullOrEmpty(secondInstruction))
            {
                Assert.AreEqual(secondInstruction, postActions[1].ManualInstructions);
            }
        }

        [TestMethod]
        public void TestPostActionInstructionsConditioning_BlankCondition()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(TestTemplateJson);
            IVariableCollection vc = new VariableCollection
            {
                ["ActionThreeCondition"] = true
            };

            FileRenameGenerator renameGenerator = new(
                    _environmentSettings,
                    "sourceName",
                    "MyProject",
                    vc,
                    []);

            List<IPostAction> postActions = PostAction.Evaluate(
                _environmentSettings,
                configModel.PostActionModels,
                vc,
                renameGenerator);

            Assert.ContainsSingle(postActions);

            Assert.AreEqual("First instruction (action 3)", postActions[0].ManualInstructions);
        }

        [TestMethod]
        public void TestPostActionInstructionsConditioning_LastTrueConditionWin()
        {
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(TestTemplateJson);
            IVariableCollection vc = new VariableCollection
            {
                ["ActionFourCondition"] = true,
                ["OperatingSystemKind"] = "Windows",
                ["Framework"] = "NET",
                ["Language"] = "C#"
            };

            FileRenameGenerator renameGenerator = new(
                    _environmentSettings,
                    "sourceName",
                    "MyProject",
                    vc,
                    []);

            List<IPostAction> postActions = PostAction.Evaluate(
                _environmentSettings,
                configModel.PostActionModels,
                vc,
                renameGenerator);

            Assert.ContainsSingle(postActions);

            Assert.AreEqual("Windows-NET-C# instructions (action 4)", postActions[0].ManualInstructions);
        }

        [TestMethod]
        public void CanReadFileRenameSettings()
        {
            string configString = /*lang=json*/ """
                {
                  "Author": "Microsoft",
                  "Classifications": "UnitTest",
                  "name": "Dotnet Unit Test Template",
                  "groupIdentity": "Dotnet.Testing",
                  "identity": "Dotnet.UnitTest.Template",
                  "shortName": "test",
                  "PostActions": [
                    {
                      "Description": "Action1",
                      "ActionId": "7F0CDCFC-839A-4625-88F6-27590E6299EF",
                      "ContinueOnError": "false",
                      "Args": {
                        "Foo": "Bar",
                        "Baz": "Blah"
                      },
                      "ApplyFileRenamesToArgs": [ "Foo" ],
                      "ApplyFileRenamesToManualInstructions": true,
                    },
                  ]
                }
                """;

            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JExtensions.ParseJsonObject(configString));

            Assert.ContainsSingle(configModel.PostActionModels);
            Assert.IsTrue(configModel.PostActionModels.Single().ApplyFileRenamesToManualInstructions);

            Assert.AreEqual("Foo", configModel.PostActionModels.Single().ApplyFileRenamesToArgs.Single());

            Assert.IsEmpty(configModel.ValidationErrors);
        }

        [TestMethod]
        public void CanAddWarningOnWrongFileRenameConfig()
        {
            string configString = /*lang=json*/ """
                {
                  "Author": "Microsoft",
                  "Classifications": "UnitTest",
                  "name": "Dotnet Unit Test Template",
                  "groupIdentity": "Dotnet.Testing",
                  "identity": "Dotnet.UnitTest.Template",
                  "shortName": "test",
                  "PostActions": [
                    {
                      "Description": "Action1",
                      "ActionId": "7F0CDCFC-839A-4625-88F6-27590E6299EF",
                      "ContinueOnError": "false",
                      "Args": {
                        "Foo": "Bar",
                        "Baz": "Blah"
                      },
                      "ApplyFileRenamesToArgs": [ "NotFoo", "Baz" ],
                      "ApplyFileRenamesToManualInstructions": true,
                    },
                  ]
                }
                """;

            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JExtensions.ParseJsonObject(configString));

            Assert.ContainsSingle(configModel.PostActionModels);
            Assert.IsTrue(configModel.PostActionModels.Single().ApplyFileRenamesToManualInstructions);

            Assert.AreEqual("Baz", configModel.PostActionModels.Single().ApplyFileRenamesToArgs.Single());

            Assert.IsNotEmpty(configModel.ValidationErrors);

            IValidationEntry validationError = configModel.ValidationErrors.Single();

            Assert.AreEqual(IValidationEntry.SeverityLevel.Warning, validationError.Severity);
            Assert.AreEqual("CONFIG0204", validationError.Code);
            Assert.AreEqual("The argument 'NotFoo' configured in 'applyFileRenamesToArgs' is not listed in 'args' and will be skipped for processing.", validationError.ErrorMessage);
        }

        [TestMethod]
        public void CanApplyFileRenames()
        {
            string configString = /*lang=json*/ """
                {
                  "Author": "Microsoft",
                  "Classifications": "UnitTest",
                  "name": "Dotnet Unit Test Template",
                  "groupIdentity": "Dotnet.Testing",
                  "identity": "Dotnet.UnitTest.Template",
                  "shortName": "test",
                  "sourceName": "SourceName",
                  "symbols": {
                    "param1": {
                        "type": "parameter",
                        "replaces": "textToReplace",
                        "fileRename": "fileToReplace"
                    }
                  },
                  "PostActions": [
                    {
                      "Description": "Action1",
                      "ActionId": "7F0CDCFC-839A-4625-88F6-27590E6299EF",
                      "ContinueOnError": "false",
                      "Args": {
                        "Foo": "fileToReplace.Bar",
                        "Baz": "SourceName.Blah"
                      },
                      "ApplyFileRenamesToArgs": [ "Foo", "Baz" ],
                      "ApplyFileRenamesToManualInstructions": true,
                      "ManualInstructions": [
                        {
                          "Text": "fileToReplace and SourceName should be changed."
                        }
                      ]
                    },
                  ]
                }
                """;

            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JExtensions.ParseJsonObject(configString));

            IVariableCollection vc = new VariableCollection
            {
                ["param1"] = "MyParam",
                ["name"] = "MyProject",

            };

            List<IReplacementTokens> filenameReplacements = new()
            {
                new ReplacementTokens("param1", "fileToReplace".TokenConfigBuilder())
            };

            FileRenameGenerator renameGenerator = new(
                _environmentSettings,
                "SourceName",
                "MyProject",
                vc,
                filenameReplacements);

            List<IPostAction> postActions = PostAction.Evaluate(
                _environmentSettings,
                configModel.PostActionModels,
                vc,
                renameGenerator);

            IPostAction postAction = Assert.ContainsSingle(postActions);

            Assert.AreEqual("MyParam.Bar", postAction.Args["Foo"]);
            Assert.AreEqual("MyProject.Blah", postAction.Args["Baz"]);
            Assert.AreEqual("MyParam and MyProject should be changed.", postAction.ManualInstructions);
        }
    }
}
