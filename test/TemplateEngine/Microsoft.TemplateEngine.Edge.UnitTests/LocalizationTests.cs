// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class LocalizationTests : TestBase
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
        [DataRow(null, "name")]
        [DataRow("de-DE", "name_de-DE:äÄßöÖüÜ")]
        [DataRow("tr-TR", "name_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedTemplateName(string? locale, string expectedName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            Assert.AreEqual(expectedName, localizationTemplate.Name);
        }

        [TestMethod]
        [DataRow(null, "desc")]
        [DataRow("de-DE", "desc_de-DE:äÄßöÖüÜ")]
        [DataRow("tr-TR", "desc_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedTemplateDescription(string? locale, string expectedDescription)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            Assert.AreEqual(expectedDescription, localizationTemplate.Description);
        }

        [TestMethod]
        [DataRow(null, "someSymbol", "sym0_displayName")]
        [DataRow("de-DE", "someSymbol", "sym0_displayName_de-DE:äÄßöÖüÜ")]
        [DataRow("tr-TR", "someSymbol", "sym0_displayName_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolDisplayName(string? locale, string symbolName, string expectedDisplayName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            ITemplateParameter? symbol = localizationTemplate.ParameterDefinitions?.FirstOrDefault(p => p.Name == symbolName);
            Assert.IsNotNull(symbol);
            Assert.AreEqual(expectedDisplayName, symbol?.DisplayName);
        }

        [TestMethod]
        [DataRow(null, "someChoice", "sym1_displayName")]
        [DataRow("de-DE", "someChoice", "sym1_displayName")]
        [DataRow("tr-TR", "someChoice", "sym1_displayName_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolChoiceDisplayName(string? locale, string symbolName, string expectedDisplayName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            ITemplateParameter? symbol = localizationTemplate.ParameterDefinitions?.FirstOrDefault(p => p.Name == symbolName);
            Assert.IsNotNull(symbol);
            Assert.AreEqual(expectedDisplayName, symbol?.DisplayName);
        }

        [TestMethod]
        [DataRow(null, "sym0_desc")]
        [DataRow("de-DE", "sym0_desc_de-DE:äÄßöÖüÜ")]
        [DataRow("tr-TR", "sym0_desc_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolDescription(string? locale, string expectedDescription)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            ITemplateParameter? symbol = localizationTemplate.ParameterDefinitions?.FirstOrDefault(p => p.Name == "someSymbol");
            Assert.IsNotNull(symbol);
            Assert.AreEqual(expectedDescription, symbol!.Description);
        }

        [TestMethod]
        [DataRow(null, "sym1_desc", "sym1_choice0", "sym1_choice1", "sym1_choice2")]
        [DataRow("de-DE", "sym1_desc_de-DE:äÄßöÖüÜ", "sym1_choice0_de-DE:äÄßöÖüÜ", "sym1_choice1_de-DE:äÄßöÖüÜ", "sym1_choice2")]
        [DataRow("tr-TR", "sym1_desc_tr-TR:çÇğĞıIİöÖşŞüÜ", "sym1_choice0_tr-TR:çÇğĞıIİöÖşŞüÜ", "sym1_choice1", "sym1_choice2_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocalizedSymbolChoices(
            string? locale,
            string symbolDesc,
            string choice0Desc,
            string choice1Desc,
            string choice2Desc)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            ITemplateParameter? symbol = localizationTemplate.ParameterDefinitions?.FirstOrDefault(p => p.Name == "someChoice");
            Assert.IsNotNull(symbol);
            Assert.AreEqual(symbolDesc, symbol!.Description);

            var choices = symbol.Choices;
            Assert.IsNotNull(choices);
            Assert.IsTrue(choices!.TryGetValue("choice0", out ParameterChoice? choice0), "Template symbol should contain a choice with name 'choice0'.");
            Assert.AreEqual(choice0Desc, choice0?.Description);
            Assert.IsTrue(choices.TryGetValue("choice1", out ParameterChoice? choice1), "Template symbol should contain a choice with name 'choice1'.");
            Assert.AreEqual(choice1Desc, choice1?.Description);
            Assert.IsTrue(choices.TryGetValue("choice2", out ParameterChoice? choice2), "Template symbol should contain a choice with name 'choice2'.");
            Assert.AreEqual(choice2Desc, choice2?.Description);
        }

        [TestMethod]
        [DataRow(0, null, "pa0_desc", "pa0_manualInstructions")]
        [DataRow(0, "de-DE", "pa0_desc_de-DE:äÄßöÖüÜ", "pa0_manualInstructions_de-DE:äÄßöÖüÜ")]
        [DataRow(0, "tr-TR", "pa0_desc_tr-TR:çÇğĞıIİöÖşŞüÜ", "pa0_manualInstructions_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        [DataRow(1, null, "pa1_desc", "pa1_manualInstructions0")]
        [DataRow(1, "de-DE", "pa1_desc_de-DE:äÄßöÖüÜ", "pa1_manualInstructions0")]
        [DataRow(1, "tr-TR", "pa1_desc_tr-TR:çÇğĞıIİöÖşŞüÜ", "pa1_manualInstructions0_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public async Task TestLocalizedPostActionFields(
            int postActionIndex,
            string? locale,
            string expectedDescription,
            string expectedManualInstructions)
        {
            var environmentSettings = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);
            var templateCreator = new TemplateCreator(environmentSettings);
            ITemplate? template = await templateCreator.LoadTemplateAsync(localizationTemplate, null, TestContext.CancellationToken);
            Assert.IsNotNull(template);
            Assert.IsNotNull(template!.Generator);

            ICreationEffects effects = await template.Generator.GetCreationEffectsAsync(
                environmentSettings,
                template,
                new ParameterSetData(template),
                Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                TestContext.CancellationToken);

            Assert.IsNotNull(effects);
            Assert.IsNotNull(effects.CreationResult);
            Assert.IsNotNull(effects.CreationResult.PostActions);
            Assert.IsGreaterThan(postActionIndex, effects.CreationResult.PostActions.Count, "Template does not contain enough post actions");
            Assert.AreEqual(expectedDescription, effects.CreationResult.PostActions[postActionIndex].Description);
            Assert.AreEqual(expectedManualInstructions, effects.CreationResult.PostActions[postActionIndex].ManualInstructions);
        }

        [TestMethod]
        [DataRow("de", "name")]
        [DataRow("de-de", "name_de-DE:äÄßöÖüÜ")]
        [DataRow("de-AT", "name")]
        [DataRow("de-at", "name")]
        [DataRow("tr", "name_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        [DataRow("tr-TR", "name_tr-TR:çÇğĞıIİöÖşŞüÜ")]
        public void TestLocaleCountryFallback(string locale, string expectedName)
        {
            _ = LoadHostWithLocalizationTemplates(locale, out _, out ITemplateInfo localizationTemplate);

            Assert.AreEqual(expectedName, localizationTemplate.Name);
        }

        private IEngineEnvironmentSettings LoadHostWithLocalizationTemplates(string? locale, out TemplatePackageManager templatePackageManager, out ITemplateInfo localizationTemplate)
        {
            var builtins = BuiltInTemplatePackagesProviderFactory.GetComponents(GetTestTemplateLocation("TemplateWithLocalization"));
            var env = s_environmentSettingsHelper.CreateEnvironment(locale: locale, additionalComponents: builtins);
            templatePackageManager = new TemplatePackageManager(env);
            IReadOnlyList<ITemplateInfo> localizedTemplates = templatePackageManager.GetTemplatesAsync(default).Result;

            Assert.IsNotEmpty(localizedTemplates, "Test template couldn't be loaded.");
            var template = localizedTemplates.FirstOrDefault(t => t.Identity == "TestAssets.TemplateWithLocalization");
            Assert.IsNotNull(template);
            localizationTemplate = template!;

            return env;
        }
    }
}
