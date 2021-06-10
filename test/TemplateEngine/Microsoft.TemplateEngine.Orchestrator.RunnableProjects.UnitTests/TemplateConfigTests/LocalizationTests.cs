// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class LocalizationTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;
        public static readonly string DefaultConfigRelativePath = ".template.config/template.json";
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
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            WriteFile(Path.Combine(tempFolder, localizationFile), fileContent, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
                Assert.NotNull(localizationModel);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)));
            }    
        }

        [Theory]
        [InlineData("{ name: \"localizedName\"}", false, "localizedName")]
        [InlineData("{ name: \"\"}", false, "")]
        [InlineData("{ notName: \"localizedName\"}", false, null)]
        public void CanReadName(string fileContent, bool errorExpected, string? expectedName)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            WriteFile(Path.Combine(tempFolder, localizationFile), fileContent, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
                Assert.NotNull(localizationModel);
                Assert.Equal(expectedName, localizationModel.Name);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)));
            }
        }

        [Theory]
        [InlineData("{ description: \"localizedDescription\"}", false, "localizedDescription")]
        [InlineData("{ description: \"\"}", false, "")]
        [InlineData("{ notdescription: \"localizedDescription\"}", false, null)]
        public void CanReadDescription(string fileContent, bool errorExpected, string? expectedDescription)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            WriteFile(Path.Combine(tempFolder, localizationFile), fileContent, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
                Assert.NotNull(localizationModel);
                Assert.Equal(expectedDescription, localizationModel.Description);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)));
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
        [InlineData("{ \"symbols/someSymbol/displayName\": \"localizedSymbol\" }", false, "someSymbol", "localizedSymbol", "(null)")]
        [InlineData("{ \"symbols/someSymbol/description\": \"localizedSymbolDescription\" }", false, "someSymbol", "(null)", "localizedSymbolDescription")]
        [InlineData("{ description: \"\"}", false, null, null, null)]
        public void CanReadNonChoiceSymbol(
            string fileContent,
            bool errorExpected,
            string expectedSymbolNamesStr,
            string expectedSymbolDisplayNamesStr,
            string expectedDescriptionsStr)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            WriteFile(Path.Combine(tempFolder, localizationFile), fileContent, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
                Assert.NotNull(localizationModel);

                if (string.IsNullOrEmpty(expectedSymbolNamesStr))
                {
                    Assert.Empty(localizationModel.ParameterSymbols);
                    return;
                }
                var expectedSymbolNames = expectedSymbolNamesStr.Split('|');
                var expectedDisplayNames = expectedSymbolDisplayNamesStr.Split('|');
                var expectedDescriptions = expectedDescriptionsStr.Split('|');

                for (int i = 0; i < expectedSymbolNames.Length; i++)
                {
                    Assert.True(localizationModel.ParameterSymbols.ContainsKey(expectedSymbolNames[i]));
                    Assert.Equal(expectedDisplayNames[i] == "(null)" ? null : expectedDisplayNames[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].DisplayName);
                    Assert.Equal(expectedDescriptions[i] == "(null)" ? null : expectedDescriptions[i], localizationModel.ParameterSymbols[expectedSymbolNames[i]].Description);
                }    
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)));
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
    "one*one-localized*(null)%two*two-localized*(null)|foo*foo-localized*(null)%bar*bar-localized*(null)"
            )]
        [InlineData("{ \"symbols/someSymbol/displayName\": \"localizedSymbol\" }", false, "someSymbol", "localizedSymbol", "(null)", "(null)")]
        [InlineData("{ \"symbols/someSymbol/description\": \"localizedSymbolDescription\" }", false, "someSymbol", "(null)", "localizedSymbolDescription", "(null)")]
        [InlineData("{ \"symbols/someSymbol/choices/one/displayName\": \"one-localized\"}", false, "someSymbol", "(null)", "(null)", "one*(null)*one-localized")]
        [InlineData("{ description: \"\"}", false, null, null, null, null)]
        public void CanReadChoiceSymbol(
            string fileContent,
            bool errorExpected,
            string expectedSymbolNamesStr,
            string expectedSymbolDisplayNamesStr,
            string expectedDescriptionsStr,
            string expectedChoicesStr)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            WriteFile(Path.Combine(tempFolder, localizationFile), fileContent, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
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
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)));
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
"first*firstLocalized%second*secondLocalized|first*firstLocalized"
    )]
        [InlineData("{ description: \"\"}", false, null, null, null)]
        [InlineData("{ \"postActions/pa0/description\": \"localizedDescription\" }", false, "pa0", "localizedDescription", "(null)")]
        [InlineData("{ \"postActions/pa0/manualInstructions/first/text\": \"localizedDescription\" }", false, "pa0", "(null)", "first*localizedDescription")]
        public void CanReadPostAction(
    string fileContent,
    bool errorExpected,
    string expectedPostActionsStr,
    string expectedDescriptionsStr,
    string expectedManualInstructionsStr)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            WriteFile(Path.Combine(tempFolder, localizationFile), fileContent, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);
            if (!errorExpected)
            {
                var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
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
                Assert.ThrowsAny<Exception>(() => LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile)));
            }
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void CanValidatePostActionWithoutLocalization()
        {
            string baseConfig =
                JsonConvert.SerializeObject(new
                {
                    postActions = new[]
                    {
                        new { id = "", description = "text", actionId = Guid.NewGuid() },
                        new { id = "pa0", description = "text", actionId = Guid.NewGuid() },
                    }
                });
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");
            WriteFile(Path.Combine(tempFolder, localizationFile), "{ \"postActions/pa0/description\": \"localizedDescription\" }", environmentSettings);
            WriteFile(Path.Combine(tempFolder, DefaultConfigRelativePath), baseConfig, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);

            var baseModel = new SimpleConfigModel(mountPoint.FileInfo(DefaultConfigRelativePath));
            var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
            Assert.True(baseModel.VerifyLocalizationModel(localizationModel, out _));

            baseModel.Localize(localizationModel);
            baseModel.PostActionModels.Single(model => model.Id == "pa0" && model.Description == "localizedDescription");
            baseModel.PostActionModels.Single(model => model.Id != "pa0" && model.Description == "text");
        }

        [Fact]
        public void CanValidatePostActionWithDefaultInstructionLocalization()
        {
            string baseConfig =
                JsonConvert.SerializeObject(new
                {
                    postActions = new[]
                    {
                        new { id = "pa0", description = "text", actionId = Guid.NewGuid(), manualInstructions = new [] { new { text = "my text" } } },
                    }
                });
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            WriteFile(Path.Combine(tempFolder, localizationFile), "{ \"postActions/pa0/manualInstructions/default/text\": \"localized\" }", environmentSettings);
            WriteFile(Path.Combine(tempFolder, DefaultConfigRelativePath), baseConfig, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);

            var baseModel = new SimpleConfigModel(mountPoint.FileInfo(DefaultConfigRelativePath));
            var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
            Assert.True(baseModel.VerifyLocalizationModel(localizationModel, out _));

            baseModel.Localize(localizationModel);
            baseModel.PostActionModels.Single(model => model.Id == "pa0" && model.ManualInstructionInfo[0].Text == "localized");
        }

        [Fact]
        public void CannotValidatePostActionWithExtraInstructionLocalization()
        {
            string baseConfig =
                JsonConvert.SerializeObject(new
                {
                    postActions = new[]
                    {
                        new
                        {
                            id = "pa0",
                            description = "text",
                            actionId = Guid.NewGuid(),
                            manualInstructions = new []
                            {
                                new { text = "my text", id = "first" },
                                new { text = "my text", id = "second" },
                            }
                        },
                    }
                });
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            WriteFile(
                Path.Combine(tempFolder, localizationFile),
                "{ \"postActions/pa0/manualInstructions/first/text\": \"localized\", \"postActions/pa0/manualInstructions/extra/text\": \"extraLoc\" }",
                environmentSettings);
            WriteFile(Path.Combine(tempFolder, DefaultConfigRelativePath), baseConfig, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);

            var baseModel = new SimpleConfigModel(mountPoint.FileInfo(DefaultConfigRelativePath));
            var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
            Assert.False(baseModel.VerifyLocalizationModel(localizationModel, out IEnumerable<string> errors));
            Assert.Equal(
                string.Format(LocalizableStrings.Authoring_InvalidManualInstructionLocalizationIndex, "extra", "pa0"),
                errors.Single());
        }

        [Fact]
        public void CannotValidateExtraPostActionLocalization()
        {
            string baseConfig =
                JsonConvert.SerializeObject(new
                {
                    postActions = new[]
                    {
                        new
                        {
                            id = "pa0",
                            description = "text",
                            actionId = Guid.NewGuid(),
                            manualInstructions = new []
                            {
                                new { text = "my text", id = "first" },
                                new { text = "my text", id = "second" },
                            }
                        },
                    }
                });
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string tempFolder = _environmentSettingsHelper.CreateTemporaryFolder();
            string localizationFile = string.Format(DefaultLocalizeConfigRelativePath, "de-DE");

            WriteFile(
                Path.Combine(tempFolder, localizationFile),
                "{ \"postActions/pa0/manualInstructions/first/text\": \"localized\", \"postActions/pa1/manualInstructions/extra/text\": \"extraLoc\" }",
                environmentSettings);
            WriteFile(Path.Combine(tempFolder, DefaultConfigRelativePath), baseConfig, environmentSettings);

            using IMountPoint mountPoint = GetMountPointForPath(tempFolder, environmentSettings);

            var baseModel = new SimpleConfigModel(mountPoint.FileInfo(DefaultConfigRelativePath));
            var localizationModel = LocalizationModelDeserializer.Deserialize(mountPoint.FileInfo(localizationFile));
            Assert.False(baseModel.VerifyLocalizationModel(localizationModel, out IEnumerable<string> errors));
            Assert.Equal(
                string.Format(LocalizableStrings.Authoring_InvalidPostActionLocalizationIndex, "pa1"),
                errors.Single());
        }

        #endregion

        internal static void WriteFile(string path, string fileContent, IEngineEnvironmentSettings engineEnvironmentSettings)
        {
            string fullPathDir = Path.GetDirectoryName(path) ?? throw new ArgumentException(nameof(path));
            engineEnvironmentSettings.Host.FileSystem.CreateDirectory(fullPathDir);
            engineEnvironmentSettings.Host.FileSystem.WriteAllText(path, fileContent ?? string.Empty);
        }

        internal static IMountPoint GetMountPointForPath(string path, IEngineEnvironmentSettings engineEnvironmentSettings)
        {
            foreach (var factory in engineEnvironmentSettings.Components.OfType<IMountPointFactory>())
            {
                if (factory.TryMount(engineEnvironmentSettings, null, path, out IMountPoint myMountPoint))
                {
                    return myMountPoint;
                }
            }
            throw new Exception($"Failed to mount the location {path}");
        }

    }
}
