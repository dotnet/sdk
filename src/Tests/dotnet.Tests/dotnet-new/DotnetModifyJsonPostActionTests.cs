// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class DotnetModifyJsonPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public DotnetModifyJsonPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact]
        public void FailsWhenNoTargetJsonFileNotFound()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            IPostAction postAction = new MockPostAction
            {
                ActionId = DotnetModifyJsonPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    ["jsonFileName"] = "nonexistingfile.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                }
            };

            Mock<IReporter> mockReporter = new Mock<IReporter>();

            mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
                        .Verifiable();

            Reporter.SetError(mockReporter.Object);

            DotnetModifyJsonPostActionProcessor processor = new DotnetModifyJsonPostActionProcessor();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);
            mockReporter.Verify(r => r.WriteLine(string.Format(Tools.New.LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "jsonFileName")), Times.Once);
        }

        [Theory]
        [MemberData(nameof(ModifyJsonPostActionTestCase<Mock<IReporter>>.InvalidConfigurationTestCases), MemberType = typeof(ModifyJsonPostActionTestCase<Mock<IReporter>>))]
        public void FailsWhenMandatoryArgumentsNotConfigured(ModifyJsonPostActionTestCase<Mock<IReporter>> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            CreateJsonFile(targetBasePath, "file.json", testCase.OriginalJsonContent);

            IPostAction postAction = new MockPostAction
            {
                ActionId = DotnetModifyJsonPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            Mock<IReporter> mockReporter = new Mock<IReporter>();

            mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
                .Verifiable();

            Reporter.SetError(mockReporter.Object);

            DotnetModifyJsonPostActionProcessor processor = new DotnetModifyJsonPostActionProcessor();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);

            testCase.AssertionCallback(mockReporter);

            //mockReporter.Verify(r => r.WriteLine(string.Format(Tools.New.LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "newJsonPropertyName")), Times.Once);

        }

        [Theory]
        [MemberData(nameof(ModifyJsonPostActionTestCase<JsonNode>.SuccessTestCases), MemberType = typeof(ModifyJsonPostActionTestCase<JsonNode>))]
        public void CanSuccessfullyModifyJsonFile(ModifyJsonPostActionTestCase<JsonNode> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string jsonFileName = testCase.PostActionArgs["jsonFileName"];

            string jsonFilePath = CreateJsonFile(targetBasePath, jsonFileName, testCase.OriginalJsonContent);

            IPostAction postAction = new MockPostAction
            {
                ActionId = DotnetModifyJsonPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            DotnetModifyJsonPostActionProcessor processor = new DotnetModifyJsonPostActionProcessor();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.True(result);

            JsonNode modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            testCase.AssertionCallback(modifiedJsonContent);
        }

        private string CreateJsonFile(string targetBasePath, string fileName, string jsonContent)
        {
            string jsonFileFullPath = Path.Combine(targetBasePath, fileName);
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(jsonFileFullPath, jsonContent);

            return jsonFileFullPath;
        }
    }

    public record ModifyJsonPostActionTestCase<TState>(
        string TestCaseDescription,
        string OriginalJsonContent,
        Dictionary<string, string> PostActionArgs,
        Action<TState> AssertionCallback)
    {
        private static readonly ModifyJsonPostActionTestCase<JsonNode>[] s_successTestCases =
        {
            new("Can add simple property",
                @"{""person"":{""name"":""bob""}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                },
                (JsonNode modifiedJsonContent) =>
                {
                    Assert.NotNull(modifiedJsonContent["person"]["lastName"]);
                    Assert.Equal("Watson", modifiedJsonContent["person"]["lastName"].ToString());
                }),

            new("Can add complex property",
                @"{""person"":{""name"":""bob""}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "address",
                    ["newJsonPropertyValue"] = @"{""street"": ""street name"", ""zip"": ""zipcode""}"
                },
                (JsonNode modifiedJsonContent) =>
                {
                    Assert.NotNull(modifiedJsonContent["person"]["address"]);
                    Assert.Equal("street name", modifiedJsonContent["person"]["address"]["street"].ToString());
                }),

            new("Can add property to document root",
                @"{""firstProperty"": ""foo""}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = null,
                    ["newJsonPropertyName"] = "secondProperty",
                    ["newJsonPropertyValue"] = "bar"
                },
                (JsonNode modifiedJsonContent) =>
                {
                    Assert.NotNull(modifiedJsonContent["secondProperty"]);
                    Assert.Equal(@"{""firstProperty"":""foo"",""secondProperty"":""bar""}", modifiedJsonContent.ToJsonString());
                })
        };

        private static readonly ModifyJsonPostActionTestCase<Mock<IReporter>>[] s_invalidConfigurationTestCases =
        {
            new("JsonFileName argument not configured",
                @"{}",
                new Dictionary<string, string>
                {
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(Tools.New.LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "jsonFileName")), Times.Once);
                }),

            new("NewJsonPropertyName argument not configured",
                @"{}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "file.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyValue"] = "Watson"
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(Tools.New.LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "newJsonPropertyName")), Times.Once);
                }),

            new("NewJsonPropertyValue argument not configured",
                @"{}",
                new Dictionary<string, string>()
                {
                    ["jsonFileName"] = "file.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "lastName"
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(Tools.New.LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "newJsonPropertyValue")), Times.Once);
                }),
        };

        public override string ToString() => TestCaseDescription;

        public static IEnumerable<object[]> SuccessTestCases()
        {
            foreach (ModifyJsonPostActionTestCase<JsonNode> testCase in s_successTestCases)
            {
                yield return new[] { testCase };
            }
        }

        public static IEnumerable<object[]> InvalidConfigurationTestCases()
        {
            foreach (ModifyJsonPostActionTestCase<Mock<IReporter>> testCase in s_invalidConfigurationTestCases)
            {
                yield return new[] { testCase };
            }
        }
    }
}
