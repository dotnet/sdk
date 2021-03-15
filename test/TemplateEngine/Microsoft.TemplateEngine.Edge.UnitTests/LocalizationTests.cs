// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class LocalizationTests
    {
        [Theory]
        [InlineData(null, "name")]
        [InlineData("de-DE", "name_de-DE:äÄßöÖüÜ")]
        [InlineData("tr-TR", "name_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedTemplateName(string locale, string expectedName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out TemplateInfo localizationTemplate);

            Assert.Equal(expectedName, localizationTemplate.Name);
        }

        [Theory]
        [InlineData(null, "desc")]
        [InlineData("de-DE", "desc_de-DE:äÄßöÖüÜ")]
        [InlineData("tr-TR", "desc_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedTemplateDescription(string locale, string expectedDescription)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out TemplateInfo localizationTemplate);

            Assert.Equal(expectedDescription, localizationTemplate.Description);
        }

        [Theory]
        [InlineData(null, "someSymbol", "sym0_displayName")]
        [InlineData("de-DE", "someSymbol", "sym0_displayName_de-DE:äÄßöÖüÜ")]
        [InlineData("tr-TR", "someSymbol", "sym0_displayName_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolDisplayName(string locale, string symbolName, string expectedDisplayName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out TemplateInfo localizationTemplate);

            KeyValuePair<string, ICacheParameter>? symbol = localizationTemplate.CacheParameters?.FirstOrDefault(p => p.Key == symbolName);
            Assert.NotNull(symbol);
            Assert.Equal(expectedDisplayName, symbol.Value.Value?.DisplayName);
        }

        [Theory]
        [InlineData(null, "someChoice", "sym1_displayName")]
        [InlineData("de-DE", "someChoice", "sym1_displayName")]
        [InlineData("tr-TR", "someChoice", "sym1_displayName")]
        public void TestLocalizedSymbolChoiceDisplayName(string locale, string symbolName, string expectedDisplayName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out TemplateInfo localizationTemplate);

            KeyValuePair<string, ICacheTag>? symbol = localizationTemplate.Tags?.FirstOrDefault(p => p.Key == symbolName);
            Assert.NotNull(symbol);
            Assert.Equal(expectedDisplayName, symbol.Value.Value?.DisplayName);
        }

        [Theory]
        [InlineData(null, "sym0_desc")]
        [InlineData("de-DE", "sym0_desc_de-DE:äÄßöÖüÜ")]
        [InlineData("tr-TR", "sym0_desc_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolDescription(string locale, string expectedDescription)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out TemplateInfo localizationTemplate);

            KeyValuePair<string, ICacheParameter>? symbol = localizationTemplate.CacheParameters?.FirstOrDefault(p => p.Key == "someSymbol");
            Assert.NotNull(symbol);
            Assert.Equal(expectedDescription, symbol.Value.Value?.Description);
        }

        [Theory]
        [InlineData(null, "sym1_desc", "sym1_choice0", "sym1_choice1", "sym1_choice2")]
        [InlineData("de-DE", "sym1_desc_de-DE:äÄßöÖüÜ", "sym1_choice0_de-DE:äÄßöÖüÜ", "sym1_choice1_de-DE:äÄßöÖüÜ", "sym1_choice2")]
        [InlineData("tr-TR", "sym1_desc_tr-TR:çÇğĞıIİöÖşŞüÜ", "sym1_choice0_tr-TR:çÇğĞıIİöÖşŞüÜ", "sym1_choice1_tr-TR:çÇğĞıIİöÖşŞüÜ", "sym1_choice2")]
        public void TestLocalizedSymbolChoices(
            string locale,
            string symbolDesc,
            string choice0Desc,
            string choice1Desc,
            string choice2Desc)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out TemplateInfo localizationTemplate);

            KeyValuePair<string, ICacheTag>? tag = localizationTemplate.Tags?.FirstOrDefault(p => p.Key == "someChoice");
            Assert.NotNull(tag);
            Assert.True(tag.HasValue);
            Assert.NotNull(tag.Value.Value);
            Assert.Equal(symbolDesc, tag.Value.Value.Description);

            var choices = tag.Value.Value.Choices;
            Assert.NotNull(choices);
            Assert.True(choices.TryGetValue("choice0", out ParameterChoice choice0), "Template symbol should contain a choice with name 'choice0'.");
            Assert.Equal(choice0Desc, choice0.Description);
            Assert.True(choices.TryGetValue("choice1", out ParameterChoice choice1), "Template symbol should contain a choice with name 'choice1'.");
            Assert.Equal(choice1Desc, choice1.Description);
            Assert.True(choices.TryGetValue("choice2", out ParameterChoice choice2), "Template symbol should contain a choice with name 'choice2'.");
            Assert.Equal(choice2Desc, choice2.Description);
        }

        [Theory]
        [InlineData(0, null, "pa0_desc", "pa0_manualInstructions")]
        [InlineData(0, "de-DE", "pa0_desc_de-DE:äÄßöÖüÜ", "pa0_manualInstructions_de-DE:äÄßöÖüÜ")]
        [InlineData(0, "tr-TR", "pa0_desc_tr-TR:çÇğĞıIİöÖşŞüÜ", "pa0_manualInstructions_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        [InlineData(1, null, "pa1_desc", "pa1_manualInstructions0")]
        [InlineData(1, "de-DE", "pa1_desc_de-DE:äÄßöÖüÜ", "pa1_manualInstructions0")]
        [InlineData(1, "tr-TR", "pa1_desc_tr-TR:çÇğĞıIİöÖşŞüÜ", "pa1_manualInstructions0_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedPostActionFields(
            int postActionIndex,
            string locale,
            string expectedDescription,
            string expectedManualInstructions)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out SettingsLoader settingsLoader, out TemplateInfo localizationTemplate);

            ITemplate template = settingsLoader.LoadTemplate(localizationTemplate, null);
            Assert.NotNull(template);
            Assert.NotNull(template.Generator);

            ICreationEffects effects = template.Generator.GetCreationEffects(
                settingsLoader.EnvironmentSettings,
                template,
                new MockParameterSet(),
                settingsLoader.Components,
                Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
                );

            Assert.NotNull(effects);
            Assert.NotNull(effects.CreationResult);
            Assert.NotNull(effects.CreationResult.PostActions);
            Assert.True(effects.CreationResult.PostActions.Count > postActionIndex, "Template does not contain enough post actions");
            Assert.Equal(expectedDescription, effects.CreationResult.PostActions[postActionIndex].Description);
            Assert.Equal(expectedManualInstructions, effects.CreationResult.PostActions[postActionIndex].ManualInstructions);
        }

        private ITemplateEngineHost LoadHostWithLocalizationTemplates(string locale, out SettingsLoader settingsLoaderOut, out TemplateInfo localizationTemplate)
        {
            var thisDir = Path.GetDirectoryName(typeof(LocalizationTests).Assembly.Location);
            var testTemplatesFolder = Path.Combine(thisDir, "..", "..", "..", "..", "..",
                "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates", "TemplateWithLocalization");

            testTemplatesFolder = new DirectoryInfo(testTemplatesFolder).FullName;

            ITemplateEngineHost host = new TestHost
            {
                HostIdentifier = "TestRunner",
                Version = "1.0.0.0",
                FallbackHostTemplateConfigNames = Array.Empty<string>(),
            };

            SettingsLoader settingsLoader = null;
            var envSettings = new EngineEnvironmentSettings(host, x => settingsLoader = new SettingsLoader(x));

            host.VirtualizeDirectory(Path.Combine(
                Environment.ExpandEnvironmentVariables("%USERPROFILE%"),
                ".templateengine",
                "TestRunner"));
            settingsLoader.Components.Register(typeof(RunnableProjectGenerator));

            settingsLoader.UserTemplateCache.Scan(testTemplatesFolder);
            settingsLoader.Save();
            settingsLoaderOut = settingsLoader;

            IReadOnlyList<TemplateInfo> localizedTemplates = settingsLoader.UserTemplateCache.GetTemplatesForLocale(
                locale,
                SettingsStore.CurrentVersion);

            Assert.True(localizedTemplates.Count != 0, "Test template couldn't be loaded.");
            localizationTemplate = localizedTemplates.FirstOrDefault(t => t.Identity == "TestAssets.TemplateWithLocalization");
            Assert.NotNull(localizationTemplate);

            return host;
        }
    }
}
