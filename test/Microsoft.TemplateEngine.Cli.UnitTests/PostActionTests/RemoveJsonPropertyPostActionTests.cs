// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

using Moq;

namespace Microsoft.TemplateEngine.Cli.UnitTests.PostActionTests
{
    public class RemoveJsonPropertyPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public RemoveJsonPropertyPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact]
        public void FailsWhenParentPropertyPathIsInvalid()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            CreateJsonFile(targetBasePath, "json.json", @"{""property1"":{""property2"":{""property3"":""foo""}}}");

            string parentPropertyPath = "property1:propertyX:property2";

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = RemoveJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = "json.json",
                    ["parentPropertyPath"] = parentPropertyPath,
                    ["jsonPropertyName"] = "property3",
                }
            };

            Mock<IReporter> mockReporter = new();

            mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
                .Verifiable();

            Reporter.SetError(mockReporter.Object);

            RemoveJsonPropertyPostActionProcessor processor = new();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);
            mockReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ParentPropertyPathInvalid, parentPropertyPath)), Times.Once);
        }

        [Fact]
        public void FailsWhenPropertyPathCasingIsNotCorrect()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string originalJsonContent = @"{""property1"":{""property2"":{""property3"":""foo""}}}";

            string jsonFilePath = CreateJsonFile(targetBasePath, "json.json", originalJsonContent);

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = RemoveJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = "json.json",
                    ["parentPropertyPath"] = "property1:Property2",
                    ["jsonPropertyName"] = "property3",
                }
            };

            RemoveJsonPropertyPostActionProcessor processor = new();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);

            Assert.Equal(originalJsonContent, _engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));
        }

        [Theory]
        [MemberData(nameof(RemoveJsonPropertyPostActionTestCase<Mock<IReporter>>.InvalidConfigurationTestCases), MemberType = typeof(RemoveJsonPropertyPostActionTestCase<Mock<IReporter>>))]
        public void FailsWhenMandatoryArgumentsNotConfigured(RemoveJsonPropertyPostActionTestCase<Mock<IReporter>> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            CreateJsonFile(targetBasePath, "file.json", testCase.OriginalJsonContent);

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = RemoveJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            Mock<IReporter> mockReporter = new();

            mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
                .Verifiable();

            Reporter.SetError(mockReporter.Object);

            RemoveJsonPropertyPostActionProcessor processor = new();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);

            testCase.AssertionCallback(mockReporter);
        }

        [Theory]
        [MemberData(nameof(RemoveJsonPropertyPostActionTestCase<(JsonNode, bool)>.SuccessTestCases), MemberType = typeof(RemoveJsonPropertyPostActionTestCase<(JsonNode, bool)>))]
        public void CanSuccessfullyModifyJsonFile(RemoveJsonPropertyPostActionTestCase<(JsonNode, bool)> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string? jsonFileName = testCase.PostActionArgs["jsonFileName"];

            Assert.NotNull(jsonFileName);

            string jsonFilePath = CreateJsonFile(targetBasePath, jsonFileName, testCase.OriginalJsonContent);

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = RemoveJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            RemoveJsonPropertyPostActionProcessor processor = new();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.True(result);

            JsonNode? modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            Assert.NotNull(modifiedJsonContent);

            testCase.AssertionCallback((modifiedJsonContent, false));
        }

        private string CreateJsonFile(string targetBasePath, string fileName, string jsonContent)
        {
            string jsonFileFullPath = Path.Combine(targetBasePath, fileName);
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(jsonFileFullPath, jsonContent);

            return jsonFileFullPath;
        }
    }

    public record RemoveJsonPropertyPostActionTestCase<TState>(
        string TestCaseDescription,
        string OriginalJsonContent,
        Dictionary<string, string> PostActionArgs,
        Action<TState> AssertionCallback)
    {
        private static readonly RemoveJsonPropertyPostActionTestCase<(JsonNode ResultingJson, bool IsNewJson)>[] _successTestCases =
        {
            new(
                "Can remove simple property",
                @"{""person"":{""name"":""bob"",""address"":{""city"":""redmond"",""zip"":""98052""}}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = "person",
                    ["jsonPropertyName"] = "name"
                },
                tuple =>
                {
                    Assert.Null(tuple.ResultingJson["person"]!["name"]);
                }),

            new(
                "Can remove complex property",
                @"{""person"":{""name"":""bob"",""address"":{""city"":""redmond"",""zip"":""98052""}}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = "person",
                    ["jsonPropertyName"] = "address",
                },
                tuple =>
                {
                    Assert.Null(tuple.ResultingJson["person"]!["address"]);
                }),

            new(
                "Can remove property at root",
                @"{""person"":{""name"":""bob"",""address"":{""city"":""redmond"",""zip"":""98052""}}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = null!,
                    ["jsonPropertyName"] = "person",
                },
                tuple =>
                {
                    Assert.Null(tuple.ResultingJson["person"]);
                }),
        };

        private static readonly RemoveJsonPropertyPostActionTestCase<Mock<IReporter>>[] _invalidConfigurationTestCases =
        {
            new(
                "JsonFileName argument not configured",
                @"{}",
                new Dictionary<string, string>
                {
                    ["parentPropertyPath"] = "person",
                    ["jsonPropertyName"] = "lastName",
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "jsonFileName")), Times.Once);
                }),

            new(
                "JsonPropertyName argument not configured",
                @"{}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "file.json",
                    ["parentPropertyPath"] = "person",
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "newJsonPropertyName")), Times.Once);
                }),
        };

        public override string ToString() => TestCaseDescription;

        public static IEnumerable<object[]> SuccessTestCases()
        {
            foreach (RemoveJsonPropertyPostActionTestCase<(JsonNode, bool)> testCase in _successTestCases)
            {
                yield return new[] { testCase };
            }
        }

        public static IEnumerable<object[]> InvalidConfigurationTestCases()
        {
            foreach (RemoveJsonPropertyPostActionTestCase<Mock<IReporter>> testCase in _invalidConfigurationTestCases)
            {
                yield return new[] { testCase };
            }
        }
    }
}
