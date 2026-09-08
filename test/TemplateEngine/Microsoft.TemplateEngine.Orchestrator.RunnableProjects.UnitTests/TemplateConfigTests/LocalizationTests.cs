// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    [TestClass]
    public class LocalizationTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();
        public static readonly string DefaultLocalizeConfigRelativePath = ".template.config/localize/templatestrings.{0}.json";

        #region Deserialization Tests

        [TestMethod]
        [DataRow("content", true)]
        [DataRow("", true)]
        [DataRow("{}", false)]
        public void CanReadLocalizationFile(string fileContent, bool errorExpected)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.IsNotNull(localizationModel);
            }
            else
            {
                Assert.Throws<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [TestMethod]
        [DataRow(/*lang=json,strict*/ """{ "name": "localizedName"}""", false, "localizedName")]
        [DataRow(/*lang=json,strict*/ """{ "name": ""}""", false, "")]
        [DataRow(/*lang=json,strict*/ """{ "notName": "localizedName"}""", false, null)]
        public void CanReadName(string fileContent, bool errorExpected, string? expectedName)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                LocalizationModel localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.IsNotNull(localizationModel);
                Assert.AreEqual(expectedName, localizationModel.Name);
            }
            else
            {
                Assert.Throws<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [TestMethod]
        [DataRow(/*lang=json,strict*/ """{ "description": "localizedDescription"}""", false, "localizedDescription")]
        [DataRow(/*lang=json,strict*/ """{ "description": ""}""", false, "")]
        [DataRow(/*lang=json,strict*/ """{ "notdescription": "localizedDescription"}""", false, null)]
        public void CanReadDescription(string fileContent, bool errorExpected, string? expectedDescription)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);
            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                LocalizationModel localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.IsNotNull(localizationModel);
                Assert.AreEqual(expectedDescription, localizationModel.Description);
            }
            else
            {
                Assert.Throws<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [TestMethod]
        [DataRow(
            "{ " +
                "\"symbols/someSymbol/displayName\": \"localizedSymbol1\", " +
                "\"symbols/someSymbol/description\": \"localizedSymbolDescription1\"," +
                "\"symbols/anotherSymbol/displayName\": \"localizedSymbol2\"," +
                "\"symbols/anotherSymbol/description\": \"localizedSymbolDescription2\"" +
            "}",
            false,
            "someSymbol|anotherSymbol",
            "localizedSymbol1|localizedSymbol2",
            "localizedSymbolDescription1|localizedSymbolDescription2")]
        [DataRow(/*lang=json,strict*/ """{ "symbols/someSymbol/displayName": "localizedSymbol" }""", false, "someSymbol", "localizedSymbol", "(null)")]
        [DataRow(/*lang=json,strict*/ """{ "symbols/someSymbol/description": "localizedSymbolDescription" }""", false, "someSymbol", "(null)", "localizedSymbolDescription")]
        [DataRow(/*lang=json,strict*/ """{ "description": ""}""", false, null, null, null)]
        // Test case for NullReferenceException fix: malformed symbol key with only "symbols/" prefix
        [DataRow(/*lang=json,strict*/ """{ "symbols/": "test" }""", false, null, null, null)]
        public void CanReadNonChoiceSymbol(
            string fileContent,
            bool errorExpected,
            string? expectedSymbolNamesStr,
            string? expectedSymbolDisplayNamesStr,
            string? expectedDescriptionsStr)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                LocalizationModel localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.IsNotNull(localizationModel);

                if (string.IsNullOrEmpty(expectedSymbolNamesStr))
                {
                    Assert.IsEmpty(localizationModel.ParameterSymbols);
                    return;
                }
                string[]? expectedSymbolNames = expectedSymbolNamesStr?.Split('|');
                string[]? expectedDisplayNames = expectedSymbolDisplayNamesStr?.Split('|');
                string[]? expectedDescriptions = expectedDescriptionsStr?.Split('|');

                for (int i = 0; i < expectedSymbolNames?.Length; i++)
                {
                    Assert.IsTrue(localizationModel.ParameterSymbols.ContainsKey(expectedSymbolNames[i]));
                    Assert.AreEqual(expectedDisplayNames?[i] == "(null)" ? null : expectedDisplayNames?[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].DisplayName);
                    Assert.AreEqual(expectedDescriptions?[i] == "(null)" ? null : expectedDescriptions?[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].Description);
                }
            }
            else
            {
                Assert.Throws<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [TestMethod]
        [DataRow(
    "{ " +
        "\"symbols/someSymbol/displayName\": \"localizedSymbol\", " +
        "\"symbols/someSymbol/description\": \"localizedSymbolDescription\"," +
        "\"symbols/someSymbol/choices/one/description\": \"one-localized\"," +
        "\"symbols/someSymbol/choices/two/description\": \"two-localized\"," +
        "\"symbols/anotherSymbol/displayName\": \"localizedSymbol\"," +
        "\"symbols/anotherSymbol/description\": \"localizedSymbolDescription\"," +
        "\"symbols/anotherSymbol/choices/foo/description\": \"foo-localized\"," +
        "\"symbols/anotherSymbol/choices/bar/description\": \"bar-localized\"" +
    "}",
    false,
    "someSymbol|anotherSymbol",
    "localizedSymbol|localizedSymbol",
    "localizedSymbolDescription|localizedSymbolDescription",
    "one*one-localized*(null)%two*two-localized*(null)|foo*foo-localized*(null)%bar*bar-localized*(null)")]
        [DataRow(/*lang=json,strict*/ """{ "symbols/someSymbol/displayName": "localizedSymbol" }""", false, "someSymbol", "localizedSymbol", "(null)", "(null)")]
        [DataRow(/*lang=json,strict*/ """{ "symbols/someSymbol/description": "localizedSymbolDescription" }""", false, "someSymbol", "(null)", "localizedSymbolDescription", "(null)")]
        [DataRow(/*lang=json,strict*/ """{ "symbols/someSymbol/choices/one/displayName": "one-localized"}""", false, "someSymbol", "(null)", "(null)", "one*(null)*one-localized")]
        [DataRow(/*lang=json,strict*/ "{ \"description\": \"\"}", false, null, null, null, null)]
        public void CanReadChoiceSymbol(
            string fileContent,
            bool errorExpected,
            string? expectedSymbolNamesStr,
            string? expectedSymbolDisplayNamesStr,
            string? expectedDescriptionsStr,
            string? expectedChoicesStr)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.IsNotNull(localizationModel);

                if (string.IsNullOrEmpty(expectedSymbolNamesStr))
                {
                    Assert.IsEmpty(localizationModel.ParameterSymbols);
                    return;
                }
                var expectedSymbolNames = expectedSymbolNamesStr?.Split('|');
                var expectedDisplayNames = expectedSymbolDisplayNamesStr?.Split('|');
                var expectedDescriptions = expectedDescriptionsStr?.Split('|');
                var expectedChoices = expectedChoicesStr?.Split('|');

                for (int i = 0; i < expectedSymbolNames?.Length; i++)
                {
                    Assert.IsTrue(localizationModel.ParameterSymbols.ContainsKey(expectedSymbolNames[i]));
                    Assert.AreEqual(expectedDisplayNames?[i] == "(null)" ? null : expectedDisplayNames?[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].DisplayName);
                    Assert.AreEqual(expectedDescriptions?[i] == "(null)" ? null : expectedDescriptions?[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].Description);

                    if (expectedChoices == null || expectedChoices[i] == "(null)")
                    {
                        Assert.IsEmpty(localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices);
                        continue;
                    }
                    var expectedChoicePairs = expectedChoices[i].Split('%');
                    foreach (var pair in expectedChoicePairs)
                    {
                        var choiceName = pair.Split('*')[0];
                        var choiceDescription = pair.Split('*')[1];
                        var choiceDisplayName = pair.Split('*')[2];
                        Assert.IsTrue(localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices.ContainsKey(choiceName));
                        Assert.AreEqual(choiceDescription == "(null)" ? null : choiceDescription, localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices[choiceName].Description);
                        Assert.AreEqual(choiceDisplayName == "(null)" ? null : choiceDisplayName, localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices[choiceName].DisplayName);
                    }
                }
            }
            else
            {
                Assert.Throws<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [TestMethod]
        [DataRow(
"{ " +
"\"postActions/pa0/description\": \"localizedDescription\", " +
"\"postActions/pa0/manualInstructions/first/text\": \"firstLocalized\"," +
"\"postActions/pa0/manualInstructions/second/text\": \"secondLocalized\"," +
"\"postActions/pa1/description\": \"localizedDescription\", " +
"\"postActions/pa1/manualInstructions/first/text\": \"firstLocalized\"," +
"}",
false,
"pa0|pa1",
"localizedDescription|localizedDescription",
"first*firstLocalized%second*secondLocalized|first*firstLocalized")]
        [DataRow(/*lang=json,strict*/ """{ "description": ""}""", false, null, null, null)]
        [DataRow(/*lang=json,strict*/ """{ "postActions/pa0/description": "localizedDescription" }""", false, "pa0", "localizedDescription", "(null)")]
        [DataRow(/*lang=json,strict*/ """{ "postActions/pa0/manualInstructions/first/text": "localizedDescription" }""", false, "pa0", "(null)", "first*localizedDescription")]
        // Test case for NullReferenceException fix: malformed postAction key with only "postActions/" prefix
        [DataRow(/*lang=json,strict*/ """{ "postActions/": "test" }""", false, null, null, null)]
        // Test case for NullReferenceException fix: malformed manualInstruction key with missing instruction id
        [DataRow(/*lang=json,strict*/ """{ "postActions/pa0/manualInstructions//text": "test" }""", false, "pa0", "(null)", "(null)")]
        public void CanReadPostAction(
    string fileContent,
    bool errorExpected,
    string? expectedPostActionsStr,
    string? expectedDescriptionsStr,
    string? expectedManualInstructionsStr)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.IsNotNull(localizationModel);

                if (string.IsNullOrEmpty(expectedPostActionsStr))
                {
                    Assert.IsEmpty(localizationModel.PostActions);
                    return;
                }
                var expectedPostActions = expectedPostActionsStr?.Split('|');
                var expectedDescriptions = expectedDescriptionsStr?.Split('|');
                var expectedInsturctions = expectedManualInstructionsStr?.Split('|');

                for (int i = 0; i < expectedPostActions?.Length; i++)
                {
                    Assert.IsTrue(localizationModel.PostActions.ContainsKey(expectedPostActions[i]));
                    Assert.AreEqual(expectedDescriptions?[i] == "(null)" ? null : expectedDescriptions?[i], localizationModel.PostActions[expectedPostActions[i]].Description);

                    if (expectedInsturctions == null || expectedInsturctions[i] == "(null)")
                    {
                        Assert.IsEmpty(localizationModel.PostActions[expectedPostActions[i]].Instructions);
                        continue;
                    }
                    var expectedInstructionPairs = expectedInsturctions[i].Split('%');
                    foreach (var pair in expectedInstructionPairs)
                    {
                        var id = pair.Split('*')[0];
                        var text = pair.Split('*')[1];
                        Assert.IsTrue(localizationModel.PostActions[expectedPostActions[i]].Instructions.ContainsKey(id));
                        Assert.AreEqual(text == "(null)" ? null : text, localizationModel.PostActions[expectedPostActions[i]].Instructions[id]);
                    }
                }
            }
            else
            {
                Assert.Throws<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        public async Task CanValidatePostActionWithoutLocalization()
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();

            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), /*lang=json,strict*/ """{ "postActions/pa0/description": "localizedDescription" }""");

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);

            TemplateConfigModel baseConfig = new TemplateConfigModel("Test")
            {
                Name = "Test",
                ShortNameList = new[] { "Test" },
                PostActionModels = new List<PostActionModel>
                {
                    new PostActionModel()
                    {
                         Id = string.Empty,
                         Description = "text",
                         ActionId = Guid.NewGuid()
                    },
                    new PostActionModel()
                    {
                         Id = "pa0",
                         Description = "text",
                         ActionId = Guid.NewGuid()
                    },
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, baseConfig.ToJsonString() },
            };
            environmentSettings.WriteTemplateSource(tempFolder, templateSourceFiles);
            RunnableProjectGenerator generator = new();

            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.IsNotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.IsNotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            Assert.IsNotNull(runnableProjectConfig.Localization);
            Assert.IsTrue(runnableProjectConfig.Localization.IsValid);

            runnableProjectConfig.Localize();
            runnableProjectConfig.ConfigurationModel.PostActionModels.Single(model => model.Id == "pa0" && model.Description == "localizedDescription");
            runnableProjectConfig.ConfigurationModel.PostActionModels.Single(model => model.Id != "pa0" && model.Description == "text");
        }

        [TestMethod]
        public async Task CanValidatePostActionWithDefaultInstructionLocalization()
        {
            TemplateConfigModel baseConfig = new TemplateConfigModel("Test")
            {
                Name = "Test",
                ShortNameList = new[] { "Test" },
                PostActionModels = new List<PostActionModel>
                {
                    new PostActionModel()
                    {
                         Id = "pa0",
                         Description = "text",
                         ActionId = Guid.NewGuid(),
                         ManualInstructionInfo = new List<ManualInstructionModel>()
                         {
                              new ManualInstructionModel(null, "my text")
                         }
                    },
                }
            };
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), /*lang=json,strict*/ """{ "postActions/pa0/manualInstructions/default/text": "localized" }""");

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, baseConfig.ToJsonString() }
            };
            environmentSettings.WriteTemplateSource(tempFolder, templateSourceFiles);

            RunnableProjectGenerator generator = new();

            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.IsNotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.IsNotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            Assert.IsNotNull(runnableProjectConfig.Localization);
            Assert.IsTrue(runnableProjectConfig.Localization.IsValid);

            runnableProjectConfig.Localize();
            runnableProjectConfig.ConfigurationModel.PostActionModels.Single(model => model.Id == "pa0" && model.ManualInstructionInfo[0].Text == "localized");
        }

        [TestMethod]
        public async Task CannotValidatePostActionWithExtraInstructionLocalization()
        {
            TemplateConfigModel baseConfig = new TemplateConfigModel("Test")
            {
                Name = "Test",
                ShortNameList = new[] { "Test" },
                PostActionModels = new List<PostActionModel>
                {
                    new PostActionModel()
                    {
                         Id = "pa0",
                         Description = "text",
                         ActionId = Guid.NewGuid(),
                         ManualInstructionInfo = new List<ManualInstructionModel>()
                         {
                              new ManualInstructionModel("first", "my text"),
                              new ManualInstructionModel("second", "my text"),
                         }
                    },
                }
            };

            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFilename = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            const string locContent = /*lang=json,strict*/
            """
            {
                "postActions/pa0/manualInstructions/first/text": "localized",
                "postActions/pa0/manualInstructions/extra/text": "extraLoc"
            }
            """;
            environmentSettings.WriteFile(
                Path.Combine(tempFolder, localizationFilename),
                locContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, baseConfig.ToJsonString() }
            };
            environmentSettings.WriteTemplateSource(tempFolder, templateSourceFiles);

            RunnableProjectGenerator generator = new();

            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.IsNotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFilename);
            Assert.IsNotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            Assert.IsNotNull(runnableProjectConfig.Localization);
            Assert.IsFalse(runnableProjectConfig.Localization.IsValid);

            IValidationEntry validationError = Assert.ContainsSingle(runnableProjectConfig.Localization.ValidationErrors);

            Assert.AreEqual(IValidationEntry.SeverityLevel.Error, validationError.Severity);
            Assert.AreEqual("LOC001", validationError.Code);
            Assert.AreEqual(
                string.Format(LocalizableStrings.Authoring_InvalidManualInstructionLocalizationIndex, "extra", "pa0"),
                validationError.ErrorMessage);
            Assert.AreEqual("/" + localizationFilename, runnableProjectConfig.Localization.File.FullPath);
        }

        [TestMethod]
        public async Task CannotValidateExtraPostActionLocalization()
        {
            TemplateConfigModel baseConfig = new TemplateConfigModel("Test")
            {
                Name = "Test",
                ShortNameList = new[] { "Test" },
                PostActionModels = new List<PostActionModel>
                {
                    new PostActionModel()
                    {
                         Id = "pa0",
                         Description = "text",
                         ActionId = Guid.NewGuid(),
                         ManualInstructionInfo = new List<ManualInstructionModel>()
                         {
                              new ManualInstructionModel("first", "my text"),
                              new ManualInstructionModel("second", "my text"),
                         }
                    },
                }
            };
            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            const string locContent = /*lang=json,strict*/
            """
            {
                "postActions/pa0/manualInstructions/first/text": "localized",
                "postActions/pa1/manualInstructions/extra/text": "extraLoc"
            }
            """;

            environmentSettings.WriteFile(
                Path.Combine(tempFolder, localizationFile),
                locContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, baseConfig.ToJsonString() }
            };
            environmentSettings.WriteTemplateSource(tempFolder, templateSourceFiles);

            RunnableProjectGenerator generator = new();

            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.IsNotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.IsNotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            Assert.IsNotNull(runnableProjectConfig.Localization);
            Assert.IsFalse(runnableProjectConfig.Localization.IsValid);

            IValidationEntry validationError = Assert.ContainsSingle(runnableProjectConfig.Localization.ValidationErrors);

            Assert.AreEqual(IValidationEntry.SeverityLevel.Error, validationError.Severity);
            Assert.AreEqual("LOC002", validationError.Code);
            Assert.AreEqual(
               string.Format(LocalizableStrings.Authoring_InvalidPostActionLocalizationIndex, "pa1"),
               validationError.ErrorMessage);
        }
        #endregion

        [TestMethod]
        public async Task CanLocalizeParameters()
        {
            TemplateConfigModel baseConfig = new TemplateConfigModel("Test")
            {
                Name = "Test",
                ShortNameList = new[] { "Test" },
                Symbols = new[]
                {
                    new ParameterSymbol("test")
                    {
                        DataType = "choice",
                        Description = "not localized",
                        DisplayName = "not localized",
                        Choices = new Dictionary<string, ParameterChoice>
                        {
                            { "choiceOne", new ParameterChoice("notLocalizedName", "notLocalizedDesc") }
                        }
                    }
                }
            };

            const string locContent = /*lang=json,strict*/
            """
            {
                "symbols/test/description": "localized description",
                "symbols/test/displayName": "localized displayName",
                "symbols/test/choices/choiceOne/displayName": "localized choiceOne displayName",
                "symbols/test/choices/choiceOne/description": "localized choiceOne description"
            }
            """;

            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            RunnableProjectGenerator generator = new();
            string tempFolder = s_environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), locContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, baseConfig.ToJsonString() }
            };
            environmentSettings.WriteTemplateSource(tempFolder, templateSourceFiles);

            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.IsNotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.IsNotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            Assert.IsNotNull(runnableProjectConfig.Localization);
            Assert.IsTrue(runnableProjectConfig.Localization.IsValid);

            runnableProjectConfig.Localize();
            ParameterSymbol actualSymbol = runnableProjectConfig.ConfigurationModel.Symbols.OfType<ParameterSymbol>().Single(s => s.Name == "test");

            Assert.AreEqual("localized displayName", actualSymbol.DisplayName);
            Assert.AreEqual("localized description", actualSymbol.Description);

            ParameterChoice? actualChoice = actualSymbol.Choices?["choiceOne"];
            Assert.IsNotNull(actualChoice);

            Assert.AreEqual("localized choiceOne displayName", actualChoice.DisplayName);
            Assert.AreEqual("localized choiceOne description", actualChoice.Description);
        }
    }
}
