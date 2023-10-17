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
    public class LocalizationTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;
        public static readonly string DefaultLocalizeConfigRelativePath = ".template.config/localize/templatestrings.{0}.json";

        public LocalizationTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        #region Deserialization Tests

        [Theory]
        [InlineData("content", true)]
        [InlineData("", true)]
        [InlineData("{}", false)]
        public void CanReadLocalizationFile(string fileContent, bool errorExpected)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.NotNull(localizationModel);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [Theory]
        [InlineData(/*lang=json*/ """{ name: "localizedName"}""", false, "localizedName")]
        [InlineData(/*lang=json*/ """{ name: ""}""", false, "")]
        [InlineData(/*lang=json*/ """{ notName: "localizedName"}""", false, null)]
        public void CanReadName(string fileContent, bool errorExpected, string? expectedName)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                LocalizationModel localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.NotNull(localizationModel);
                Assert.Equal(expectedName, localizationModel.Name);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [Theory]
        [InlineData(/*lang=json*/ """{ description: "localizedDescription"}""", false, "localizedDescription")]
        [InlineData(/*lang=json*/ """{ description: ""}""", false, "")]
        [InlineData(/*lang=json*/ """{ notdescription: "localizedDescription"}""", false, null)]
        public void CanReadDescription(string fileContent, bool errorExpected, string? expectedDescription)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);
            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                LocalizationModel localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.NotNull(localizationModel);
                Assert.Equal(expectedDescription, localizationModel.Description);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [Theory]
        [InlineData(
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
        [InlineData(/*lang=json,strict*/ """{ "symbols/someSymbol/displayName": "localizedSymbol" }""", false, "someSymbol", "localizedSymbol", "(null)")]
        [InlineData(/*lang=json,strict*/ """{ "symbols/someSymbol/description": "localizedSymbolDescription" }""", false, "someSymbol", "(null)", "localizedSymbolDescription")]
        [InlineData(/*lang=json*/ """{ description: ""}""", false, null, null, null)]
        public void CanReadNonChoiceSymbol(
            string fileContent,
            bool errorExpected,
            string expectedSymbolNamesStr,
            string expectedSymbolDisplayNamesStr,
            string expectedDescriptionsStr)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                LocalizationModel localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.NotNull(localizationModel);

                if (string.IsNullOrEmpty(expectedSymbolNamesStr))
                {
                    Assert.Empty(localizationModel.ParameterSymbols);
                    return;
                }
                string[] expectedSymbolNames = expectedSymbolNamesStr.Split('|');
                string[] expectedDisplayNames = expectedSymbolDisplayNamesStr.Split('|');
                string[] expectedDescriptions = expectedDescriptionsStr.Split('|');

                for (int i = 0; i < expectedSymbolNames.Length; i++)
                {
                    Assert.True(localizationModel.ParameterSymbols.ContainsKey(expectedSymbolNames[i]));
                    Assert.Equal(expectedDisplayNames[i] == "(null)" ? null : expectedDisplayNames[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].DisplayName);
                    Assert.Equal(expectedDescriptions[i] == "(null)" ? null : expectedDescriptions[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].Description);
                }
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [Theory]
        [InlineData(
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
        [InlineData(/*lang=json,strict*/ """{ "symbols/someSymbol/displayName": "localizedSymbol" }""", false, "someSymbol", "localizedSymbol", "(null)", "(null)")]
        [InlineData(/*lang=json,strict*/ """{ "symbols/someSymbol/description": "localizedSymbolDescription" }""", false, "someSymbol", "(null)", "localizedSymbolDescription", "(null)")]
        [InlineData(/*lang=json,strict*/ """{ "symbols/someSymbol/choices/one/displayName": "one-localized"}""", false, "someSymbol", "(null)", "(null)", "one*(null)*one-localized")]
        [InlineData(/*lang=json*/ "{ description: \"\"}", false, null, null, null, null)]
        public void CanReadChoiceSymbol(
            string fileContent,
            bool errorExpected,
            string expectedSymbolNamesStr,
            string expectedSymbolDisplayNamesStr,
            string expectedDescriptionsStr,
            string expectedChoicesStr)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.NotNull(localizationModel);

                if (string.IsNullOrEmpty(expectedSymbolNamesStr))
                {
                    Assert.Empty(localizationModel.ParameterSymbols);
                    return;
                }
                var expectedSymbolNames = expectedSymbolNamesStr.Split('|');
                var expectedDisplayNames = expectedSymbolDisplayNamesStr.Split('|');
                var expectedDescriptions = expectedDescriptionsStr.Split('|');
                var expectedChoices = expectedChoicesStr?.Split('|');

                for (int i = 0; i < expectedSymbolNames.Length; i++)
                {
                    Assert.True(localizationModel.ParameterSymbols.ContainsKey(expectedSymbolNames[i]));
                    Assert.Equal(expectedDisplayNames[i] == "(null)" ? null : expectedDisplayNames[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].DisplayName);
                    Assert.Equal(expectedDescriptions[i] == "(null)" ? null : expectedDescriptions[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].Description);

                    if (expectedChoices == null || expectedChoices[i] == "(null)")
                    {
                        Assert.Empty(localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices);
                        continue;
                    }
                    var expectedChoicePairs = expectedChoices[i].Split('%');
                    foreach (var pair in expectedChoicePairs)
                    {
                        var choiceName = pair.Split('*')[0];
                        var choiceDescription = pair.Split('*')[1];
                        var choiceDisplayName = pair.Split('*')[2];
                        Assert.True(localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices.ContainsKey(choiceName));
                        Assert.Equal(choiceDescription == "(null)" ? null : choiceDescription, localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices[choiceName].Description);
                        Assert.Equal(choiceDisplayName == "(null)" ? null : choiceDisplayName, localizationModel.ParameterSymbols[expectedSymbolNames[i]].Choices[choiceName].DisplayName);
                    }
                }
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        [Theory]
        [InlineData(
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
        [InlineData(/*lang=json*/ """{ description: ""}""", false, null, null, null)]
        [InlineData(/*lang=json,strict*/ """{ "postActions/pa0/description": "localizedDescription" }""", false, "pa0", "localizedDescription", "(null)")]
        [InlineData(/*lang=json,strict*/ """{ "postActions/pa0/manualInstructions/first/text": "localizedDescription" }""", false, "pa0", "(null)", "first*localizedDescription")]
        public void CanReadPostAction(
    string fileContent,
    bool errorExpected,
    string expectedPostActionsStr,
    string expectedDescriptionsStr,
    string expectedManualInstructionsStr)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), fileContent);

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!);
                Assert.NotNull(localizationModel);

                if (string.IsNullOrEmpty(expectedPostActionsStr))
                {
                    Assert.Empty(localizationModel.PostActions);
                    return;
                }
                var expectedPostActions = expectedPostActionsStr.Split('|');
                var expectedDescriptions = expectedDescriptionsStr.Split('|');
                var expectedInsturctions = expectedManualInstructionsStr?.Split('|');

                for (int i = 0; i < expectedPostActions.Length; i++)
                {
                    Assert.True(localizationModel.PostActions.ContainsKey(expectedPostActions[i]));
                    Assert.Equal(expectedDescriptions[i] == "(null)" ? null : expectedDescriptions[i], localizationModel.PostActions[expectedPostActions[i]].Description);

                    if (expectedInsturctions == null || expectedInsturctions[i] == "(null)")
                    {
                        Assert.Empty(localizationModel.PostActions[expectedPostActions[i]].Instructions);
                        continue;
                    }
                    var expectedInstructionPairs = expectedInsturctions[i].Split('%');
                    foreach (var pair in expectedInstructionPairs)
                    {
                        var id = pair.Split('*')[0];
                        var text = pair.Split('*')[1];
                        Assert.True(localizationModel.PostActions[expectedPostActions[i]].Instructions.ContainsKey(id));
                        Assert.Equal(text == "(null)" ? null : text, localizationModel.PostActions[expectedPostActions[i]].Instructions[id]);
                    }
                }
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)!));
            }
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task CanValidatePostActionWithoutLocalization()
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
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
            Assert.NotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.NotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, default);

            Assert.NotNull(runnableProjectConfig.Localization);
            Assert.True(runnableProjectConfig.Localization.IsValid);

            runnableProjectConfig.Localize();
            runnableProjectConfig.ConfigurationModel.PostActionModels.Single(model => model.Id == "pa0" && model.Description == "localizedDescription");
            runnableProjectConfig.ConfigurationModel.PostActionModels.Single(model => model.Id != "pa0" && model.Description == "text");
        }

        [Fact]
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
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = environmentSettings.GetTempVirtualizedPath();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            environmentSettings.WriteFile(Path.Combine(tempFolder, localizationFile), /*lang=json,strict*/ """{ "postActions/pa0/manualInstructions/default/text": "localized" }""");

            using IMountPoint mountPoint = environmentSettings.MountPath(tempFolder);

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, baseConfig.ToJsonString() }
            };
            environmentSettings.WriteTemplateSource(tempFolder, templateSourceFiles);

            RunnableProjectGenerator generator = new();

            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.NotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, default);

            Assert.NotNull(runnableProjectConfig.Localization);
            Assert.True(runnableProjectConfig.Localization.IsValid);

            runnableProjectConfig.Localize();
            runnableProjectConfig.ConfigurationModel.PostActionModels.Single(model => model.Id == "pa0" && model.ManualInstructionInfo[0].Text == "localized");
        }

        [Fact]
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
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
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
            Assert.NotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFilename);
            Assert.NotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, default);

            Assert.NotNull(runnableProjectConfig.Localization);
            Assert.False(runnableProjectConfig.Localization.IsValid);

            IValidationEntry validationError = Assert.Single(runnableProjectConfig.Localization.ValidationErrors);

            Assert.Equal(IValidationEntry.SeverityLevel.Error, validationError.Severity);
            Assert.Equal("LOC001", validationError.Code);
            Assert.Equal(
                string.Format(LocalizableStrings.Authoring_InvalidManualInstructionLocalizationIndex, "extra", "pa0"),
                validationError.ErrorMessage);
            Assert.Equal("/" + localizationFilename, runnableProjectConfig.Localization.File.FullPath);
        }

        [Fact]
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
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
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
            Assert.NotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.NotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, default);

            Assert.NotNull(runnableProjectConfig.Localization);
            Assert.False(runnableProjectConfig.Localization.IsValid);

            IValidationEntry validationError = Assert.Single(runnableProjectConfig.Localization.ValidationErrors);

            Assert.Equal(IValidationEntry.SeverityLevel.Error, validationError.Severity);
            Assert.Equal("LOC002", validationError.Code);
            Assert.Equal(
               string.Format(LocalizableStrings.Authoring_InvalidPostActionLocalizationIndex, "pa1"),
               validationError.ErrorMessage);
        }
        #endregion

        [Fact]
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

            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            RunnableProjectGenerator generator = new();
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
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
            Assert.NotNull(templateConfigFile);
            IFile? locFile = mountPoint.FileInfo(localizationFile);
            Assert.NotNull(locFile);

            using var runnableProjectConfig = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile, localeConfigFile: locFile);
            await runnableProjectConfig.ValidateAsync(ValidationScope.Instantiation, default);

            Assert.NotNull(runnableProjectConfig.Localization);
            Assert.True(runnableProjectConfig.Localization.IsValid);

            runnableProjectConfig.Localize();
            ParameterSymbol actualSymbol = runnableProjectConfig.ConfigurationModel.Symbols.OfType<ParameterSymbol>().Single(s => s.Name == "test");

            Assert.Equal("localized displayName", actualSymbol.DisplayName);
            Assert.Equal("localized description", actualSymbol.Description);

            ParameterChoice? actualChoice = actualSymbol.Choices?["choiceOne"];
            Assert.NotNull(actualChoice);

            Assert.Equal("localized choiceOne displayName", actualChoice.DisplayName);
            Assert.Equal("localized choiceOne description", actualChoice.Description);
        }
    }
}
