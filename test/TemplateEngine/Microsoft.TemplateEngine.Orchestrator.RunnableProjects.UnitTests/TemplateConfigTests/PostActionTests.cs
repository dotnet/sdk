// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class PostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;

        public PostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettings = environmentSettingsHelper.CreateEnvironment(virtualize: true);
        }

        private static JObject TestTemplateJson
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
                  ]
                }
                """;
                return JObject.Parse(configString);
            }
        }

        [Theory(DisplayName = nameof(TestPostActionConditioning))]
        [InlineData(true, true, 2, new[] { "Action1", "Default instructions (action 1)" }, new[] { "Action2", "Default instructions (action 2)" })]
        [InlineData(true, false, 1, new[] { "Action1", "Default instructions (action 1)" }, null)]
        [InlineData(false, true, 1, new[] { "Action2", "Default instructions (action 2)" }, null)]
        [InlineData(false, false, 0, null, null)]
        public void TestPostActionConditioning(bool condition1, bool condition2, int expectedActionCount, string[] firstResult, string[] secondResult)
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
                Array.Empty<IReplacementTokens>());

            List<IPostAction> postActions = PostAction.Evaluate(
                _environmentSettings,
                configModel.PostActionModels,
                vc,
                renameGenerator);

            Assert.Equal(expectedActionCount, postActions.Count);
            if (firstResult != null && firstResult.Length > 0)
            {
                Assert.True(string.Equals(postActions[0].Description, firstResult[0]), $"expected '{firstResult[0]}', but got {postActions[0].Description}");
                Assert.Equal(firstResult[1], postActions[0].ManualInstructions);
            }

            if (secondResult != null && secondResult.Length > 0)
            {
                Assert.True(string.Equals(postActions[1].Description, secondResult[0]), $"expected '{secondResult[0]}', but got {postActions[1].Description}");
                Assert.Equal(secondResult[1], postActions[1].ManualInstructions);
            }
        }

        [Theory(DisplayName = nameof(TestPostActionInstructionsConditioning))]
        [InlineData(true, true, 2, "Windows", "Windows instructions (action 1)", "Windows instructions (action 2)")]
        [InlineData(true, true, 2, "Linux", "Linux instructions (action 1)", "Linux instructions (action 2)")]
        [InlineData(true, true, 2, "Mac", "Mac instructions (action 1)", "Mac instructions (action 2)")]
        [InlineData(true, true, 2, "BeOS", "Default instructions (action 1)", "Default instructions (action 2)")]
        [InlineData(true, false, 1, "Windows", "Windows instructions (action 1)", null)]
        [InlineData(true, false, 1, "Linux", "Linux instructions (action 1)", null)]
        [InlineData(true, false, 1, "Mac", "Mac instructions (action 1)", null)]
        [InlineData(true, false, 1, "BeOS", "Default instructions (action 1)", null)]
        [InlineData(false, true, 1, "Windows", "Windows instructions (action 2)", null)]
        [InlineData(false, true, 1, "Linux", "Linux instructions (action 2)", null)]
        [InlineData(false, true, 1, "Mac", "Mac instructions (action 2)", null)]
        [InlineData(false, true, 1, "BeOS", "Default instructions (action 2)", null)]
        public void TestPostActionInstructionsConditioning(bool condition1, bool condition2, int expectedActionCount, string operatingSystemValue, string firstInstruction, string secondInstruction)
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
                    Array.Empty<IReplacementTokens>());

            List<IPostAction> postActions = PostAction.Evaluate(
                _environmentSettings,
                configModel.PostActionModels,
                vc,
                renameGenerator);

            Assert.Equal(expectedActionCount, postActions.Count);

            if (!string.IsNullOrEmpty(firstInstruction))
            {
                Assert.Equal(firstInstruction, postActions[0].ManualInstructions);
            }

            if (!string.IsNullOrEmpty(secondInstruction))
            {
                Assert.Equal(secondInstruction, postActions[1].ManualInstructions);
            }
        }

        [Fact]
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

            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.Parse(configString));

            Assert.Equal(1, configModel.PostActionModels.Count);
            Assert.True(configModel.PostActionModels.Single().ApplyFileRenamesToManualInstructions);

            Assert.Equal("Foo", configModel.PostActionModels.Single().ApplyFileRenamesToArgs.Single());

            Assert.Empty(configModel.ValidationErrors);
        }

        [Fact]
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

            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.Parse(configString));

            Assert.Equal(1, configModel.PostActionModels.Count);
            Assert.True(configModel.PostActionModels.Single().ApplyFileRenamesToManualInstructions);

            Assert.Equal("Baz", configModel.PostActionModels.Single().ApplyFileRenamesToArgs.Single());

            Assert.NotEmpty(configModel.ValidationErrors);

            IValidationEntry validationError = configModel.ValidationErrors.Single();

            Assert.Equal(IValidationEntry.SeverityLevel.Warning, validationError.Severity);
            Assert.Equal("CONFIG0204", validationError.Code);
            Assert.Equal("The argument 'NotFoo' configured in 'applyFileRenamesToArgs' is not listed in 'args' and will be skipped for processing.", validationError.ErrorMessage);
        }

        [Fact]
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

            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.Parse(configString));

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

            IPostAction postAction = Assert.Single(postActions);

            Assert.Equal("MyParam.Bar", postAction.Args["Foo"]);
            Assert.Equal("MyProject.Blah", postAction.Args["Baz"]);
            Assert.Equal("MyParam and MyProject should be changed.", postAction.ManualInstructions);
        }
    }
}
