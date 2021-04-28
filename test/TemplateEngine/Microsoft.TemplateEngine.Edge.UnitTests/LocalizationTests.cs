// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class LocalizationTests : IDisposable
    {
        private EnvironmentSettingsHelper _helper = new EnvironmentSettingsHelper();

        [Theory]
        [InlineData(null, "name")]
        [InlineData("de-DE", "name_de-DE:äÄßöÖüÜ")]
        [InlineData("tr-TR", "name_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedTemplateName(string locale, string expectedName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            Assert.Equal(expectedName, localizationTemplate.Name);
        }

        [Theory]
        [InlineData(null, "desc")]
        [InlineData("de-DE", "desc_de-DE:äÄßöÖüÜ")]
        [InlineData("tr-TR", "desc_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedTemplateDescription(string locale, string expectedDescription)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            Assert.Equal(expectedDescription, localizationTemplate.Description);
        }

        [Theory]
        [InlineData(null, "someSymbol", "sym0_displayName")]
        [InlineData("de-DE", "someSymbol", "sym0_displayName_de-DE:äÄßöÖüÜ")]
        [InlineData("tr-TR", "someSymbol", "sym0_displayName_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolDisplayName(string locale, string symbolName, string expectedDisplayName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            KeyValuePair<string, ICacheParameter>? symbol = localizationTemplate.CacheParameters?.FirstOrDefault(p => p.Key == symbolName);
            Assert.NotNull(symbol);
            Assert.Equal(expectedDisplayName, symbol.Value.Value?.DisplayName);
        }

        [Theory]
        [InlineData(null, "someChoice", "sym1_displayName")]
        [InlineData("de-DE", "someChoice", "sym1_displayName")]
        [InlineData("tr-TR", "someChoice", "sym1_displayName_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolChoiceDisplayName(string locale, string symbolName, string expectedDisplayName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

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
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            KeyValuePair<string, ICacheParameter>? symbol = localizationTemplate.CacheParameters?.FirstOrDefault(p => p.Key == "someSymbol");
            Assert.NotNull(symbol);
            Assert.Equal(expectedDescription, symbol.Value.Value?.Description);
        }

        [Theory]
        [InlineData(null, "sym1_desc", "sym1_choice0", "sym1_choice1", "sym1_choice2")]
        [InlineData("de-DE", "sym1_desc_de-DE:äÄßöÖüÜ", "sym1_choice0_de-DE:äÄßöÖüÜ", "sym1_choice1_de-DE:äÄßöÖüÜ", "sym1_choice2")]
        [InlineData("tr-TR", "sym1_desc_tr-TR:çÇğĞıIİöÖşŞüÜ", "sym1_choice0_tr-TR:çÇğĞıIİöÖşŞüÜ", "sym1_choice1", "sym1_choice2_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolChoices(
            string locale,
            string symbolDesc,
            string choice0Desc,
            string choice1Desc,
            string choice2Desc)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

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
            _ = LoadHostWithLocalizationTemplates(locale, out ISettingsLoader settingsLoader, out ITemplateInfo localizationTemplate);

            ITemplate template = settingsLoader.LoadTemplate(localizationTemplate, null);
            Assert.NotNull(template);
            Assert.NotNull(template.Generator);

            ICreationEffects effects = template.Generator.GetCreationEffects(
                settingsLoader.EnvironmentSettings,
                template,
                template.Generator.GetParametersForTemplate(settingsLoader.EnvironmentSettings, template),
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

        [Theory]
        [InlineData("de", "name_de-DE:äÄßöÖüÜ")]
        [InlineData("de-de", "name_de-DE:äÄßöÖüÜ")]
        [InlineData("de-Au", "name_de-DE:äÄßöÖüÜ")]
        [InlineData("de-AU", "name_de-DE:äÄßöÖüÜ")]
        public void TestLocaleCountryFallback(string locale, string expectedName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            Assert.Equal(expectedName, localizationTemplate.Name);
        }

        public void Dispose() => _helper.Dispose();

        private ITemplateEngineHost LoadHostWithLocalizationTemplates(string locale, out ISettingsLoader settingsLoaderOut, out ITemplateInfo localizationTemplate)
        {
            var env = _helper.CreateEnvironment(locale);
            env.SettingsLoader.Components.Register(typeof(TemplatesFactory));
            settingsLoaderOut = env.SettingsLoader;

            IReadOnlyList<ITemplateInfo> localizedTemplates = settingsLoaderOut.GetTemplatesAsync(default).Result;

            Assert.True(localizedTemplates.Count != 0, "Test template couldn't be loaded.");
            localizationTemplate = localizedTemplates.FirstOrDefault(t => t.Identity == "TestAssets.TemplateWithLocalization");
            Assert.NotNull(localizationTemplate);

            return env.Host;
        }

        private class TemplatesFactory : ITemplatePackageProviderFactory
        {
            public string DisplayName => nameof(LocalizationTests);

            public Guid Id => new Guid("{3DB0E733-6411-4898-B500-65B122309A9B}");

            public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings) =>
                new DefaultTemplatePackageProvider(this, settings, null, new[]
                {
                    Path.Combine(
                        Path.GetDirectoryName(typeof(LocalizationTests).Assembly.Location),
                        "..",
                        "..",
                        "..",
                        "..",
                        "..",
                        "test",
                        "Microsoft.TemplateEngine.TestTemplates",
                        "test_templates",
                        "TemplateWithLocalization")
                });
        }
    }
}
