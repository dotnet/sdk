// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    [CollectionDefinition("Localization Tests")]
    public class LocalizationTestsCollection
    {
        //this collection is used in order that the tests are run sequentially, as they change UI localization.
    }

    [Collection("Localization Tests")]
    public class LocalizationTests : BootstrapperTestBase
    {
        [Fact]
        public async Task SkipsLocalizationOnInstall_WhenInvalidFormat()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            using Bootstrapper bootstrapper = GetBootstrapper(addLoggerProviders: new[] { loggerProvider });
            string testTemplateLocation = GetTestTemplateLocation("Invalid/Localization/InvalidFormat");

            List<InstallRequest> installRequests = new() { new InstallRequest(testTemplateLocation) };
            IReadOnlyList<InstallResult> installationResults = await bootstrapper.InstallTemplatePackagesAsync(installRequests);
            Assert.True(installationResults.Single().Success);

            loggedMessages.Clear();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(default);
            Assert.Single(foundTemplates);

            (LogLevel level, string message) = Assert.Single(loggedMessages.Where(m => m.Level == LogLevel.Warning));
            Assert.Contains("Failed to read or parse localization file", message);
            Assert.Contains("localize/templatestrings.de-DE.json", message);
        }

        [Fact]
        public async Task SkipsLocalizationOnInstall_WhenLocalizationValidationFails()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            using Bootstrapper bootstrapper = GetBootstrapper(addLoggerProviders: new[] { loggerProvider });
            string testTemplateLocation = GetTestTemplateLocation("Invalid/Localization/ValidationFailure");

            string[] expectedErrors = new[]
            {
                """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation errors in 'de-DE' localization:
                   [Error][LOC001] In localization file under the post action with id 'pa1', there are localized strings for manual instruction(s) with ids 'do-not-exist'. These manual instructions do not exist in the template.json file and should be removed from localization file.
                   [Error][LOC002] Post action(s) with id(s) 'pa0' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.
        
                """,
                """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation errors in 'tr' localization:
                   [Error][LOC002] Post action(s) with id(s) 'pa6' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.
        
                """
            };

            string[] expectedWarnings = new[]
            {
                """
                The template 'name' (TestAssets.Invalid.Localization.ValidationFailure) has the following validation warnings:
                   [Warning][CONFIG0201] Id of the post action 'pa2' at index '3' is not unique. Only the first post action that uses this id will be localized.

                """,
                "Failed to load the 'de-DE' localization the template 'name' (TestAssets.Invalid.Localization.ValidationFailure): the localization file is not valid. The localization will be skipped.",
                "Failed to load the 'tr' localization the template 'name' (TestAssets.Invalid.Localization.ValidationFailure): the localization file is not valid. The localization will be skipped."
            };

            List<InstallRequest> installRequests = new() { new InstallRequest(testTemplateLocation) };
            IReadOnlyList<InstallResult> installationResults = await bootstrapper.InstallTemplatePackagesAsync(installRequests);
            Assert.True(installationResults.Single().Success);

            loggedMessages.Clear();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(default);
            Assert.Single(foundTemplates);

            var errors = loggedMessages.Where(m => m.Level == LogLevel.Error).Select(m => m.Message);
            var warnings = loggedMessages.Where(m => m.Level == LogLevel.Warning).Select(m => m.Message);

            Assert.Equal(expectedErrors.Length, errors.Count());
            Assert.Equal(expectedWarnings.Length, warnings.Count());

            foreach (string error in expectedErrors)
            {
                Assert.Contains(error, errors);
            }
            foreach (string warning in expectedWarnings)
            {
                Assert.Contains(warning, warnings);
            }
        }

        [Fact]
        public async Task SkipsLocalizationOnInstantiate_WhenInvalidFormat()
        {
            var oldCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
                List<(LogLevel Level, string Message)> loggedMessages = new();
                InMemoryLoggerProvider loggerProvider = new(loggedMessages);

                using Bootstrapper bootstrapper = GetBootstrapper(addLoggerProviders: new[] { loggerProvider });
                string validTestTemplateLocation = GetTestTemplateLocation("TemplateWithLocalization");
                string invalidTestTemplateLocation = GetTestTemplateLocation("Invalid/Localization/InvalidFormat");

                string tmpTemplateLocation = TestUtils.CreateTemporaryFolder();
                string output = TestUtils.CreateTemporaryFolder();
                TestUtils.DirectoryCopy(validTestTemplateLocation, tmpTemplateLocation, copySubDirs: true);

                List<InstallRequest> installRequests = new() { new InstallRequest(tmpTemplateLocation) };
                IReadOnlyList<InstallResult> installationResults = await bootstrapper.InstallTemplatePackagesAsync(installRequests);
                Assert.True(installationResults.Single().Success);

                loggedMessages.Clear();
                var foundTemplates = await bootstrapper.GetTemplatesAsync(default);
                var templateToRun = Assert.Single(foundTemplates);

                Assert.DoesNotContain(loggedMessages, m => m.Level == LogLevel.Error);
                Assert.DoesNotContain(loggedMessages, m => m.Level == LogLevel.Warning);

                loggedMessages.Clear();

                //replace localization with bad file
                File.Copy(
                    Path.Combine(invalidTestTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                    Path.Combine(tmpTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                    overwrite: true);

                var result = await bootstrapper.CreateAsync(templateToRun, "MyApp.1", output, new Dictionary<string, string?>());

                Assert.True(result.Status == Edge.Template.CreationResultStatus.Success);

                var errors = loggedMessages.Where(m => m.Level == LogLevel.Error).Select(m => m.Message);
                var warnings = loggedMessages.Where(m => m.Level == LogLevel.Warning).Select(m => m.Message);

                Assert.Empty(errors);
                string warning = Assert.Single(warnings);
                Assert.Equal($"Fehler beim Lesen oder parsen der Lokalisierungsdatei {tmpTemplateLocation}{Path.DirectorySeparatorChar}.template.config/localize/templatestrings.de-DE.json. Sie wird bei der weiteren Verarbeitung übersprungen.", warning);
            }
            finally
            {
                CultureInfo.CurrentUICulture = oldCulture;
            }
        }

        [Fact]
        public async Task SkipsLocalizationOnInstantiate_WhenLocalizationValidationFails()
        {
            var oldCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
                List<(LogLevel Level, string Message)> loggedMessages = new();
                InMemoryLoggerProvider loggerProvider = new(loggedMessages);

                using Bootstrapper bootstrapper = GetBootstrapper(addLoggerProviders: new[] { loggerProvider });
                string validTestTemplateLocation = GetTestTemplateLocation("TemplateWithLocalization");
                string invalidTestTemplateLocation = GetTestTemplateLocation("Invalid/Localization/ValidationFailure");
                string tmpTemplateLocation = TestUtils.CreateTemporaryFolder();
                string output = TestUtils.CreateTemporaryFolder();
                TestUtils.DirectoryCopy(validTestTemplateLocation, tmpTemplateLocation, copySubDirs: true);

                string[] expectedErrors = new[]
                {
                    """
                    Die Vorlage 'name' (TestAssets.TemplateWithLocalization) weist die folgenden Überprüfungsfehler in Lokalisierung „de-DE“ auf:
                       [Error][LOC001] In der Lokalisierungsdatei unter der POST-Aktion mit der ID „pa1“ befinden sich lokalisierte Zeichenfolgen für manuelle Anweisungen mit den IDs „do-not-exist“. Diese manuellen Anweisungen sind in der Datei „template.json“ nicht vorhanden und sollten aus der Lokalisierungsdatei entfernt werden.

                    """
                };

                string[] expectedWarnings = new[]
                {
                    "Lokalisierung „de-DE“ der Vorlage 'name' (TestAssets.TemplateWithLocalization) konnte nicht geladen werden: Die Lokalisierungsdatei ist ungültig. Die Lokalisierung wird übersprungen.",
                };
                List<InstallRequest> installRequests = new() { new InstallRequest(tmpTemplateLocation) };
                IReadOnlyList<InstallResult> installationResults = await bootstrapper.InstallTemplatePackagesAsync(installRequests);
                Assert.True(installationResults.Single().Success);

                loggedMessages.Clear();
                var foundTemplates = await bootstrapper.GetTemplatesAsync(default);
                var templateToRun = Assert.Single(foundTemplates);

                Assert.DoesNotContain(loggedMessages, m => m.Level == LogLevel.Error);
                Assert.DoesNotContain(loggedMessages, m => m.Level == LogLevel.Warning);

                loggedMessages.Clear();

                //replace localization with bad file
                File.Copy(
                Path.Combine(invalidTestTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                Path.Combine(tmpTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                overwrite: true);

                var result = await bootstrapper.CreateAsync(templateToRun, "MyApp.1", output, new Dictionary<string, string?>());

                Assert.True(result.Status == Edge.Template.CreationResultStatus.Success);

                var errors = loggedMessages.Where(m => m.Level == LogLevel.Error).Select(m => m.Message);
                var warnings = loggedMessages.Where(m => m.Level == LogLevel.Warning).Select(m => m.Message);

                Assert.Equal(expectedErrors.Length, errors.Count());
                Assert.Equal(expectedWarnings.Length, warnings.Count());

                foreach (string error in expectedErrors)
                {
                    Assert.Contains(error, errors);
                }
                foreach (string warning in expectedWarnings)
                {
                    Assert.Contains(warning, warnings);
                }

            }
            finally
            {
                CultureInfo.CurrentUICulture = oldCulture;
            }
        }
    }

}
