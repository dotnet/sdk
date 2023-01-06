// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
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

        [Theory]
        [MemberData(nameof(ModifyJsonPostActionTestCase.TestCases), MemberType = typeof(ModifyJsonPostActionTestCase))]
        public void X(ModifyJsonPostActionTestCase testCase)
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

    public record ModifyJsonPostActionTestCase(
        string TestCaseDescription,
        string OriginalJsonContent,
        Dictionary<string, string> PostActionArgs,
        Action<JsonNode> AssertionCallback)
    {
        private static readonly ModifyJsonPostActionTestCase[] s_testCases =
        {
            new("Can add simple property",
                @"{""person"":{""name"":""bob""}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentProperty"] = "person",
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
                    ["parentProperty"] = "person",
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
                    ["parentProperty"] = null,
                    ["newJsonPropertyName"] = "secondProperty",
                    ["newJsonPropertyValue"] = "bar"
                },
                (JsonNode modifiedJsonContent) =>
                {
                    Assert.NotNull(modifiedJsonContent["secondProperty"]);
                    Assert.Equal(@"{""firstProperty"":""foo"",""secondProperty"":""bar""}", modifiedJsonContent.ToJsonString());
                })
        };

        public override string ToString() => TestCaseDescription;

        public static IEnumerable<object[]> TestCases()
        {
            foreach (ModifyJsonPostActionTestCase testCase in s_testCases)
            {
                yield return new[] { testCase };
            }
        }
    }
}
