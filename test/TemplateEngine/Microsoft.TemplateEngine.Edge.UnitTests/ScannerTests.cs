// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class ScannerTests : TestBase, IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _settingsHelper;

        public ScannerTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _settingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task CanLogValidationMessagesOnInstall_MissingIdentity()
        {
            string templatesLocation = Path.Combine(TestTemplatesLocation, "Invalid", "MissingIdentity");

            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = _settingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            Scanner scanner = new(settings);

            ScanResult result = await scanner.ScanAsync(templatesLocation, default);

            Assert.Empty(result.Templates);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Empty(result.Localizations);
#pragma warning restore CS0618 // Type or member is obsolete

            string errorMessage = Assert.Single(loggedMessages, l => l.Level is LogLevel.Error).Message;
            Assert.Equal($"Failed to load template from {Path.GetFullPath(templatesLocation) + Path.DirectorySeparatorChar}.template.config/template.json.{Environment.NewLine}Details: 'identity' is missing or is an empty string.", errorMessage);
        }

        [Fact]
        public async Task CanLogValidationMessagesOnInstall_ErrorsInTemplateConfig()
        {
            string templatesLocation = Path.Combine(TestTemplatesLocation, "Invalid", "MissingMandatoryConfig");

            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = _settingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            Scanner scanner = new(settings);

            ScanResult result = await scanner.ScanAsync(templatesLocation, default);

            Assert.Empty(result.Templates);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Empty(result.Localizations);
#pragma warning restore CS0618 // Type or member is obsolete

            List<string> errorMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Error).Select(e => e.Message).ToList();
            Assert.Equal(2, errorMessages.Count);

            List<string> warningMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Warning).Select(e => e.Message).ToList();
            Assert.Empty(warningMessages);

            List<string> debugMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Debug).Select(e => e.Message).ToList();
            Assert.Equal(4, debugMessages.Count);

            Assert.Equal(
                """
                The template '<no name>' (MissingConfigTest) has the following validation errors:
                   [Error][MV002] Missing 'name'.
                   [Error][MV003] Missing 'shortName'.

                """,
                errorMessages[0]);
            Assert.Equal("Failed to load the template '<no name>' (MissingConfigTest): the template is not valid.", errorMessages[1]);

            Assert.Contains(
                """
                The template '<no name>' (MissingConfigTest) has the following validation messages:
                   [Info][MV005] Missing 'sourceName'.
                   [Info][MV006] Missing 'author'.
                   [Info][MV007] Missing 'groupIdentity'.
                   [Info][MV008] Missing 'generatorVersions'.
                   [Info][MV009] Missing 'precedence'.
                   [Info][MV010] Missing 'classifications'.

                """,
                debugMessages);
        }

        [Fact]
        public async Task CanLogValidationMessagesOnInstall_Localization()
        {
            string templatesLocation = Path.Combine(TestTemplatesLocation, "Invalid", "Localization", "ValidationFailure");

            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = _settingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            Scanner scanner = new(settings);

            ScanResult result = await scanner.ScanAsync(templatesLocation, default);

            Assert.Single(result.Templates);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Empty(result.Localizations);
#pragma warning restore CS0618 // Type or member is obsolete

            List<string> errorMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Error).Select(e => e.Message).OrderBy(em => em).ToList();

            Assert.Equal(2, errorMessages.Count);

            List<string> warningMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Warning).Select(e => e.Message).OrderBy(em => em).ToList();

            Assert.Equal(3, warningMessages.Count);

            Assert.Equal(
               """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation errors in 'de-DE' localization:
                   [Error][LOC001] In localization file under the post action with id 'pa1', there are localized strings for manual instruction(s) with ids 'do-not-exist'. These manual instructions do not exist in the template.json file and should be removed from localization file.
                   [Error][LOC002] Post action(s) with id(s) 'pa0' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.

                """,
               errorMessages[0]);
            Assert.Equal(
                """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation errors in 'tr' localization:
                   [Error][LOC002] Post action(s) with id(s) 'pa6' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.

                """,
                errorMessages[1]);

            Assert.Equal("Failed to load the 'de-DE' localization the template 'name' (TestAssets.Invalid.Localization.ValidationFailure): the localization file is not valid. The localization will be skipped.", warningMessages[0]);
            Assert.Equal("Failed to load the 'tr' localization the template 'name' (TestAssets.Invalid.Localization.ValidationFailure): the localization file is not valid. The localization will be skipped.", warningMessages[1]);

            Assert.Equal(
        """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation warnings:
                   [Warning][CONFIG0201] Id of the post action 'pa2' at index '3' is not unique. Only the first post action that uses this id will be localized.

                """,
        warningMessages[2]);
        }
    }
}
