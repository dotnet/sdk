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
    [TestClass]
    public class AddJsonPropertyPostActionTests
    {
        // MSTest has no IClassFixture equivalent; a lazily-initialized static helper
        // mirrors the per-class lifetime that xUnit's IClassFixture provides.
        private static readonly Lazy<EnvironmentSettingsHelper> s_environmentSettingsHelper =
            new(() => new EnvironmentSettingsHelper());

        private IEngineEnvironmentSettings _engineEnvironmentSettings = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.Value.CreateEnvironment(
                hostIdentifier: GetType().Name,
                virtualize: true);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_environmentSettingsHelper.IsValueCreated)
            {
                s_environmentSettingsHelper.Value.Dispose();
            }
        }

        [TestMethod]
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

            Assert.IsFalse(result);
            mockReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_ModifyJson_Error_ParentPropertyPathInvalid, parentPropertyPath)), Times.Once);
        }

        [TestMethod]
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

            Assert.IsFalse(result);

            Assert.AreEqual(originalJsonContent, _engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));
        }

        [TestMethod]
        [DynamicData(nameof(ModifyJsonPostActionTestCase<Mock<IReporter>>.InvalidConfigurationTestCases), typeof(ModifyJsonPostActionTestCase<Mock<IReporter>>))]
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

            Assert.IsFalse(result);

            testCase.AssertionCallback(mockReporter);
        }

        [TestMethod]
        [DynamicData(nameof(ModifyJsonPostActionTestCase<(JsonNode, bool)>.SuccessTestCases), typeof(ModifyJsonPostActionTestCase<(JsonNode, bool)>))]
        public void CanSuccessfullyModifyJsonFile(ModifyJsonPostActionTestCase<(JsonNode, bool)> testCase)
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string? jsonFileName = testCase.PostActionArgs["jsonFileName"];

            Assert.IsNotNull(jsonFileName);

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

            Assert.IsTrue(result);

            JsonNode? modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            Assert.IsNotNull(modifiedJsonContent);

            testCase.AssertionCallback((modifiedJsonContent, false));
        }

        [TestMethod]
        [DynamicData(nameof(ModifyJsonPostActionTestCase<(JsonNode, bool)>.SuccessTestCases), typeof(ModifyJsonPostActionTestCase<(JsonNode, bool)>))]
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

            Assert.IsTrue(result);

            string jsonFilePath = Path.Combine(targetBasePath, jsonFileName);
            JsonNode? modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            Assert.IsNotNull(modifiedJsonContent);

            testCase.AssertionCallback((modifiedJsonContent, true));
        }

        [TestMethod]
        [DynamicData(nameof(ModifyJsonPostActionTestCase<(JsonNode, bool)>.SuccessTestCases), typeof(ModifyJsonPostActionTestCase<(JsonNode, bool)>))]
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

            Assert.IsTrue(result);

            JsonNode? modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            Assert.IsNotNull(modifiedJsonContent);

            testCase.AssertionCallback((modifiedJsonContent, true));
        }

        [TestMethod]
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

            Assert.IsFalse(result);

            Assert.AreEqual(originalJsonContent, _engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));
        }

        [TestMethod]
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

            Assert.IsFalse(result);

            string jsonFilePath = Path.Combine(targetBasePath, jsonFileName);
            Assert.IsFalse(_engineEnvironmentSettings.Host.FileSystem.FileExists(jsonFilePath));
        }

        [TestMethod]
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

            Assert.IsFalse(result);

            string jsonFilePath = Path.Combine(targetBasePath, jsonFileName);
            Assert.AreEqual("{}", _engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));
        }

        [TestMethod]
        public void RepoRootDetectionShouldPreferGlobalJson_NoSlnInvolved()
        {
            var physicalFileSystem = _engineEnvironmentSettings.Host.FileSystem;
            var tempPath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            var dirWithGlobalJson = Path.Combine(tempPath, nameof(RepoRootDetectionShouldPreferGlobalJson_NoSlnInvolved));
            physicalFileSystem.CreateDirectory(dirWithGlobalJson);
            physicalFileSystem.WriteAllText(Path.Combine(dirWithGlobalJson, "global.json"), "{}");
            var subDir = Path.Combine(dirWithGlobalJson, "subdir");
            physicalFileSystem.CreateDirectory(subDir);
            AddJsonPropertyPostActionProcessor.GetRootDirectory(physicalFileSystem, subDir).Should().Be(dirWithGlobalJson);
        }

        [TestMethod]
        public void RepoRootDetectionShouldPreferGlobalJson_SlnInSubDirectory()
        {
            var physicalFileSystem = _engineEnvironmentSettings.Host.FileSystem;
            var tempPath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            var dirWithGlobalJson = Path.Combine(tempPath, nameof(RepoRootDetectionShouldPreferGlobalJson_SlnInSubDirectory));
            physicalFileSystem.CreateDirectory(dirWithGlobalJson);
            physicalFileSystem.WriteAllText(Path.Combine(dirWithGlobalJson, "global.json"), "{}");
            var subDir = Path.Combine(dirWithGlobalJson, "subdir");
            physicalFileSystem.CreateDirectory(subDir);
            physicalFileSystem.WriteAllText(Path.Combine(subDir, "MySolution.sln"), "{}");
            AddJsonPropertyPostActionProcessor.GetRootDirectory(physicalFileSystem, subDir).Should().Be(dirWithGlobalJson);
        }

        [TestMethod]
        public void RepoRootDetectionShouldPreferGlobalJson_SlnInParent()
        {
            var physicalFileSystem = _engineEnvironmentSettings.Host.FileSystem;
            var tempPath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            var dirWithGlobalJson = Path.Combine(tempPath, nameof(RepoRootDetectionShouldPreferGlobalJson_SlnInParent));
            physicalFileSystem.CreateDirectory(dirWithGlobalJson);
            physicalFileSystem.WriteAllText(Path.Combine(dirWithGlobalJson, "global.json"), "{}");
            var subDir = Path.Combine(dirWithGlobalJson, "subdir");
            physicalFileSystem.CreateDirectory(subDir);
            physicalFileSystem.WriteAllText(Path.Combine(tempPath, "MySolution.sln"), "{}");
            AddJsonPropertyPostActionProcessor.GetRootDirectory(physicalFileSystem, subDir).Should().Be(dirWithGlobalJson);
        }

        [TestMethod]
        public void RepoRootDetectionShouldPreferGitDirectory_NoSlnInvolved()
        {
            var physicalFileSystem = _engineEnvironmentSettings.Host.FileSystem;
            var tempPath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            var dirWithGitDirectory = Path.Combine(tempPath, nameof(RepoRootDetectionShouldPreferGitDirectory_NoSlnInvolved));
            physicalFileSystem.CreateDirectory(dirWithGitDirectory);
            physicalFileSystem.CreateDirectory(Path.Combine(dirWithGitDirectory, ".git"));
            var subDir = Path.Combine(dirWithGitDirectory, "subdir");
            physicalFileSystem.CreateDirectory(subDir);
            AddJsonPropertyPostActionProcessor.GetRootDirectory(physicalFileSystem, subDir).Should().Be(dirWithGitDirectory);
        }

        [TestMethod]
        public void RepoRootDetectionShouldPreferGitDirectory_SlnInSubDirectory()
        {
            var physicalFileSystem = _engineEnvironmentSettings.Host.FileSystem;
            var tempPath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            var dirWithGitDirectory = Path.Combine(tempPath, nameof(RepoRootDetectionShouldPreferGitDirectory_SlnInSubDirectory));
            physicalFileSystem.CreateDirectory(dirWithGitDirectory);
            physicalFileSystem.CreateDirectory(Path.Combine(dirWithGitDirectory, ".git"));
            var subDir = Path.Combine(dirWithGitDirectory, "subdir");
            physicalFileSystem.CreateDirectory(subDir);
            physicalFileSystem.WriteAllText(Path.Combine(subDir, "MySolution.sln"), "");
            AddJsonPropertyPostActionProcessor.GetRootDirectory(physicalFileSystem, subDir).Should().Be(dirWithGitDirectory);
        }

        [TestMethod]
        public void RepoRootDetectionShouldPreferGitDirectory_SlnInParent()
        {
            var physicalFileSystem = _engineEnvironmentSettings.Host.FileSystem;
            var tempPath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            var dirWithGitDirectory = Path.Combine(tempPath, nameof(RepoRootDetectionShouldPreferGitDirectory_SlnInParent));
            physicalFileSystem.CreateDirectory(dirWithGitDirectory);
            physicalFileSystem.CreateDirectory(Path.Combine(dirWithGitDirectory, ".git"));
            var subDir = Path.Combine(dirWithGitDirectory, "subdir");
            physicalFileSystem.CreateDirectory(subDir);
            physicalFileSystem.WriteAllText(Path.Combine(tempPath, "MySolution.sln"), "");
            AddJsonPropertyPostActionProcessor.GetRootDirectory(physicalFileSystem, subDir).Should().Be(dirWithGitDirectory);
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
                    Assert.AreEqual("Watson", tuple.ResultingJson["person"]!["lastName"]!.ToString());
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
                    Assert.AreEqual("street name", tuple.ResultingJson["person"]!["address"]!["street"]!.ToString());
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
                        Assert.AreEqual(@"{""secondProperty"":""bar""}", tuple.ResultingJson.ToJsonString());
                    }
                    else
                    {
                        Assert.AreEqual(@"{""firstProperty"":""foo"",""secondProperty"":""bar""}", tuple.ResultingJson.ToJsonString());
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
                        Assert.AreEqual(@"{""rootProperty"":{""subProperty1"":{""subProperty2"":{""subProperty3"":{""foo"":""bar""}}}}}", tuple.ResultingJson.ToJsonString());
                    }
                    else
                    {
                        Assert.AreEqual(@"{""rootProperty"":{""subProperty1"":{""subProperty2"":{""subProperty3"":{""name"":""test"",""foo"":""bar""}}}}}", tuple.ResultingJson.ToJsonString());
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
