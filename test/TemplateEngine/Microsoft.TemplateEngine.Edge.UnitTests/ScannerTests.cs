// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class ScannerTests : TestBase
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_settingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_settingsHelper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_settingsHelper?.Dispose();

        [TestMethod]
        public async Task CanLogValidationMessagesOnInstall_MissingIdentity()
        {
            string templatesLocation = Path.Combine(TestTemplatesLocation, "Invalid", "MissingIdentity");

            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = s_settingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            Scanner scanner = new(settings);

            ScanResult result = await scanner.ScanAsync(templatesLocation, TestContext.CancellationToken);

            Assert.IsEmpty(result.Templates);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsEmpty(result.Localizations);
#pragma warning restore CS0618 // Type or member is obsolete

            string errorMessage = Assert.ContainsSingle(l => l.Level is LogLevel.Error, loggedMessages).Message;
            Assert.AreEqual($"Failed to load template from {Path.GetFullPath(templatesLocation) + Path.DirectorySeparatorChar}.template.config/template.json.{Environment.NewLine}Details: 'identity' is missing or is an empty string.", errorMessage);
        }

        [TestMethod]
        public async Task CanLogValidationMessagesOnInstall_ErrorsInTemplateConfig()
        {
            string templatesLocation = Path.Combine(TestTemplatesLocation, "Invalid", "MissingMandatoryConfig");

            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = s_settingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            Scanner scanner = new(settings);

            ScanResult result = await scanner.ScanAsync(templatesLocation, TestContext.CancellationToken);

            Assert.IsEmpty(result.Templates);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsEmpty(result.Localizations);
#pragma warning restore CS0618 // Type or member is obsolete

            List<string> errorMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Error).Select(e => e.Message).ToList();
            Assert.HasCount(2, errorMessages);

            List<string> warningMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Warning).Select(e => e.Message).ToList();
            Assert.IsEmpty(warningMessages);

            List<string> debugMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Debug).Select(e => e.Message).ToList();
            Assert.HasCount(4, debugMessages);

            Assert.AreEqual(
                """
                The template '<no name>' (MissingConfigTest) has the following validation errors:
                   [Error][MV002] Missing 'name'.
                   [Error][MV003] Missing 'shortName'.

                """,
                errorMessages[0]);
            Assert.AreEqual("Failed to load the template '<no name>' (MissingConfigTest): the template is not valid.", errorMessages[1]);

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

        [TestMethod]
        public async Task CanLogValidationMessagesOnInstall_Localization()
        {
            string templatesLocation = Path.Combine(TestTemplatesLocation, "Invalid", "Localization", "ValidationFailure");

            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = s_settingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            Scanner scanner = new(settings);

            ScanResult result = await scanner.ScanAsync(templatesLocation, TestContext.CancellationToken);

            Assert.ContainsSingle(result.Templates);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsEmpty(result.Localizations);
#pragma warning restore CS0618 // Type or member is obsolete

            List<string> errorMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Error).Select(e => e.Message).OrderBy(em => em).ToList();

            Assert.HasCount(2, errorMessages);

            List<string> warningMessages = loggedMessages.Where(lm => lm.Level == LogLevel.Warning).Select(e => e.Message).OrderBy(em => em).ToList();

            Assert.HasCount(3, warningMessages);

            Assert.AreEqual(
               """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation errors in 'de-DE' localization:
                   [Error][LOC001] In localization file under the post action with id 'pa1', there are localized strings for manual instruction(s) with ids 'do-not-exist'. These manual instructions do not exist in the template.json file and should be removed from localization file.
                   [Error][LOC002] Post action(s) with id(s) 'pa0' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.

                """,
               errorMessages[0]);
            Assert.AreEqual(
                """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation errors in 'tr' localization:
                   [Error][LOC002] Post action(s) with id(s) 'pa6' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.

                """,
                errorMessages[1]);

            Assert.AreEqual("Failed to load the 'de-DE' localization the template 'name' (TestAssets.Invalid.Localization.ValidationFailure): the localization file is not valid. The localization will be skipped.", warningMessages[0]);
            Assert.AreEqual("Failed to load the 'tr' localization the template 'name' (TestAssets.Invalid.Localization.ValidationFailure): the localization file is not valid. The localization will be skipped.", warningMessages[1]);

            Assert.AreEqual(
        """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation warnings:
                   [Warning][CONFIG0201] Id of the post action 'pa2' at index '3' is not unique. Only the first post action that uses this id will be localized.

                """,
        warningMessages[2]);
        }
    }
}
