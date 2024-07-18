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
    public class AddJsonPropertyPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public AddJsonPropertyPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
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
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = "json.json",
                    ["parentPropertyPath"] = parentPropertyPath,
                    ["newJsonPropertyName"] = "bar",
                    ["newJsonPropertyValue"] = "test"
                }
            };

            Mock<IReporter> mockReporter = new();

            mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
                .Verifiable();

            Reporter.SetError(mockReporter.Object);

            AddJsonPropertyPostActionProcessor processor = new();

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
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = "json.json",
                    ["parentPropertyPath"] = "property1:Property2",
                    ["newJsonPropertyName"] = "bar",
                    ["newJsonPropertyValue"] = "test"
                }
            };

            AddJsonPropertyPostActionProcessor processor = new();

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
        [MemberData(nameof(ModifyJsonPostActionTestCase<Mock<IReporter>>.InvalidConfigurationTestCases), MemberType = typeof(ModifyJsonPostActionTestCase<Mock<IReporter>>))]
        public void FailsWhenMandatoryArgumentsNotConfigured(ModifyJsonPostActionTestCase<Mock<IReporter>> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            CreateJsonFile(targetBasePath, "file.json", testCase.OriginalJsonContent);

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            Mock<IReporter> mockReporter = new();

            mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
                .Verifiable();

            Reporter.SetError(mockReporter.Object);

            AddJsonPropertyPostActionProcessor processor = new();

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
        [MemberData(nameof(ModifyJsonPostActionTestCase<(JsonNode, bool)>.SuccessTestCases), MemberType = typeof(ModifyJsonPostActionTestCase<(JsonNode, bool)>))]
        public void CanSuccessfullyModifyJsonFile(ModifyJsonPostActionTestCase<(JsonNode, bool)> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string? jsonFileName = testCase.PostActionArgs["jsonFileName"];

            Assert.NotNull(jsonFileName);

            string jsonFilePath = CreateJsonFile(targetBasePath, jsonFileName, testCase.OriginalJsonContent);

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            AddJsonPropertyPostActionProcessor processor = new();

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

        [Theory]
        [MemberData(nameof(ModifyJsonPostActionTestCase<(JsonNode, bool)>.SuccessTestCases), MemberType = typeof(ModifyJsonPostActionTestCase<(JsonNode, bool)>))]
        public void CanSuccessfullyCreateAndModifyJsonFileWhenAllowFileCreationAndPathCreationAreSet(ModifyJsonPostActionTestCase<(JsonNode, bool)> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string jsonFileName = Guid.NewGuid().ToString("N") + ".json";
            testCase.PostActionArgs["jsonFileName"] = jsonFileName;
            testCase.PostActionArgs["allowFileCreation"] = "true";
            testCase.PostActionArgs["allowPathCreation"] = "true";
            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            AddJsonPropertyPostActionProcessor processor = new();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.True(result);

            string jsonFilePath = Path.Combine(targetBasePath, jsonFileName);
            JsonNode? modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            Assert.NotNull(modifiedJsonContent);

            testCase.AssertionCallback((modifiedJsonContent, true));
        }

        [Theory]
        [MemberData(nameof(ModifyJsonPostActionTestCase<(JsonNode, bool)>.SuccessTestCases), MemberType = typeof(ModifyJsonPostActionTestCase<(JsonNode, bool)>))]
        public void CanSuccessfullyModifyJsonFileWhenPathDoesNotExistAndAllowPathCreationIsSet(ModifyJsonPostActionTestCase<(JsonNode, bool)> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string jsonFileName = Guid.NewGuid().ToString("N") + ".json";
            testCase.PostActionArgs["jsonFileName"] = jsonFileName;
            testCase.PostActionArgs["allowPathCreation"] = "true";

            string jsonFilePath = CreateJsonFile(targetBasePath, jsonFileName, "{}");

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = testCase.PostActionArgs
            };

            AddJsonPropertyPostActionProcessor processor = new();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.True(result);

            JsonNode? modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            Assert.NotNull(modifiedJsonContent);

            testCase.AssertionCallback((modifiedJsonContent, true));
        }

        [Fact]
        public void FailsWhenFileExistsButPathDoesNotExistAndAllowPathCreationIsNotSet()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string jsonFileName = Guid.NewGuid().ToString("N") + ".json";
            string originalJsonContent = "{}";
            string jsonFilePath = CreateJsonFile(targetBasePath, jsonFileName, originalJsonContent);

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = jsonFileName,
                    ["allowPathCreation"] = "false",
                    ["parentPropertyPath"] = "",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                }
            };

            AddJsonPropertyPostActionProcessor processor = new();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);

            Assert.Equal(originalJsonContent, _engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));
        }

        [Fact]
        public void FailsWhenFileDoesNotExistAndAllowFileCreationIsNotSet()
        {
            string jsonFileName = Guid.NewGuid().ToString("N") + ".json";

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = jsonFileName,
                    ["allowFileCreation"] = "false",
                    ["parentPropertyPath"] = "",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                }
            };

            AddJsonPropertyPostActionProcessor processor = new();

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);

            string jsonFilePath = Path.Combine(targetBasePath, jsonFileName);
            Assert.False(_engineEnvironmentSettings.Host.FileSystem.FileExists(jsonFilePath));
        }

        [Fact]
        public void FailsWhenFileDoesNotExistAndAllowFileCreationIsSetButAllowPathCreationIsNotSet()
        {
            string jsonFileName = Guid.NewGuid().ToString("N") + ".json";

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = AddJsonPropertyPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = jsonFileName,
                    ["allowFileCreation"] = "true",
                    ["allowPathCreation"] = "false",
                    ["parentPropertyPath"] = "",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                }
            };

            AddJsonPropertyPostActionProcessor processor = new();

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);

            string jsonFilePath = Path.Combine(targetBasePath, jsonFileName);
            Assert.Equal("{}", _engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));
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
        private static readonly ModifyJsonPostActionTestCase<(JsonNode ResultingJson, bool IsNewJson)>[] _successTestCases =
        {
            new(
                "Can add simple property",
                @"{""person"":{""name"":""bob""}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                },
                tuple =>
                {
                    Assert.Equal("Watson", tuple.ResultingJson["person"]!["lastName"]!.ToString());
                }),

            new(
                "Can add complex property",
                @"{""person"":{""name"":""bob""}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "address",
                    ["newJsonPropertyValue"] = @"{""street"": ""street name"", ""zip"": ""zipcode""}"
                },
                tuple =>
                {
                    Assert.Equal("street name", tuple.ResultingJson["person"]!["address"]!["street"]!.ToString());
                }),

            new(
                "Can add property to document root",
                @"{""firstProperty"": ""foo""}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = null!,
                    ["newJsonPropertyName"] = "secondProperty",
                    ["newJsonPropertyValue"] = "bar"
                },
                tuple =>
                {
                    if (tuple.IsNewJson)
                    {
                        Assert.Equal(@"{""secondProperty"":""bar""}", tuple.ResultingJson.ToJsonString());
                    }
                    else
                    {
                        Assert.Equal(@"{""firstProperty"":""foo"",""secondProperty"":""bar""}", tuple.ResultingJson.ToJsonString());
                    }
                }),

            new(
                "Can add property to sub-property",
                @"{""rootProperty"": {""subProperty1"": {""subProperty2"":{""subProperty3"":{""name"":""test""}}}}}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentPropertyPath"] = "rootProperty:subProperty1:subProperty2:subProperty3",
                    ["newJsonPropertyName"] = "foo",
                    ["newJsonPropertyValue"] = "bar"
                },
                tuple =>
                {
                    if (tuple.IsNewJson)
                    {
                        Assert.Equal(@"{""rootProperty"":{""subProperty1"":{""subProperty2"":{""subProperty3"":{""foo"":""bar""}}}}}", tuple.ResultingJson.ToJsonString());
                    }
                    else
                    {
                        Assert.Equal(@"{""rootProperty"":{""subProperty1"":{""subProperty2"":{""subProperty3"":{""name"":""test"",""foo"":""bar""}}}}}", tuple.ResultingJson.ToJsonString());
                    }
                })
        };

        private static readonly ModifyJsonPostActionTestCase<Mock<IReporter>>[] _invalidConfigurationTestCases =
        {
            new(
                "JsonFileName argument not configured",
                @"{}",
                new Dictionary<string, string>
                {
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "jsonFileName")), Times.Once);
                }),

            new(
                "NewJsonPropertyName argument not configured",
                @"{}",
                new Dictionary<string, string>
                {
                    ["jsonFileName"] = "file.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyValue"] = "Watson"
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "newJsonPropertyName")), Times.Once);
                }),

            new(
                "NewJsonPropertyValue argument not configured",
                @"{}",
                new Dictionary<string, string>()
                {
                    ["jsonFileName"] = "file.json",
                    ["parentPropertyPath"] = "person",
                    ["newJsonPropertyName"] = "lastName"
                },
                (Mock<IReporter> errorReporter) =>
                {
                    errorReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ArgumentNotConfigured, "newJsonPropertyValue")), Times.Once);
                }),
        };

        public override string ToString() => TestCaseDescription;

        public static IEnumerable<object[]> SuccessTestCases()
        {
            foreach (ModifyJsonPostActionTestCase<(JsonNode, bool)> testCase in _successTestCases)
            {
                yield return new[] { testCase };
            }
        }

        public static IEnumerable<object[]> InvalidConfigurationTestCases()
        {
            foreach (ModifyJsonPostActionTestCase<Mock<IReporter>> testCase in _invalidConfigurationTestCases)
            {
                yield return new[] { testCase };
            }
        }
    }
}
