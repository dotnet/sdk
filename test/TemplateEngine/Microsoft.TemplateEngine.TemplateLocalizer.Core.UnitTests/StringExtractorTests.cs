// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.UnitTests
{
    public class StringExtractorTests
    {
        [Fact]
        public void FirstLevelStringsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(strings, s => s.Identifier == "//name" && s.LocalizationKey == "name" && s.Value == "name");
            Assert.Contains(strings, s => s.Identifier == "//description" && s.LocalizationKey == "description" && s.Value == "desc");
        }

        [Fact]
        public void CertainStringsAreOmitted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.DoesNotContain(strings, s => s.Identifier == "//$schema" || s.LocalizationKey == "$schema");
            Assert.DoesNotContain(strings, s => s.Identifier == "//classification" || s.LocalizationKey == "classification");
            Assert.DoesNotContain(strings, s => s.Identifier == "//groupIdentity" || s.LocalizationKey == "groupIdentity");
        }

        [Fact]
        public void DefaultAuthoringLanguageIsEnglish()
        {
            _ = ExtractStrings(GetTestTemplateJsonContent(), out string language);

            Assert.Equal("en", language);
        }

        [Fact]
        public void SymbolsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(strings, s => s.Identifier == "//symbols/somesymbol/description" && s.LocalizationKey == "symbols/someSymbol/description" && s.Value == "sym0_desc");
            Assert.Contains(strings, s => s.Identifier == "//symbols/somesymbol/displayname" && s.LocalizationKey == "symbols/someSymbol/displayName" && s.Value == "sym0_displayName");
            Assert.Contains(strings, s => s.Identifier == "//symbols/somechoice/description" && s.LocalizationKey == "symbols/someChoice/description" && s.Value == "sym1_desc");
            Assert.Contains(strings, s => s.Identifier == "//symbols/somechoice/displayname" && s.LocalizationKey == "symbols/someChoice/displayName" && s.Value == "sym1_displayName");
        }

        [Fact]
        public void SymbolChoicesAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(strings, s => s.Identifier == "//symbols/somechoice/choices/0/description" && s.LocalizationKey == "symbols/someChoice/choices/choice0/description" && s.Value == "sym1_choice0");
            Assert.Contains(strings, s => s.Identifier == "//symbols/somechoice/choices/0/displayname" && s.LocalizationKey == "symbols/someChoice/choices/choice0/displayName" && s.Value == "sym1_choice0_displayName");
            Assert.Contains(strings, s => s.Identifier == "//symbols/somechoice/choices/2/description" && s.LocalizationKey == "symbols/someChoice/choices/choice2/description" && s.Value == "sym1_choice2");
            Assert.Contains(strings, s => s.Identifier == "//symbols/somechoice/choices/2/displayname" && s.LocalizationKey == "symbols/someChoice/choices/choice2/displayName" && s.Value == "sym1_choice2_displayName");
        }

        [Fact]
        public void PostActionsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(strings, s => s.Identifier == "//postactions/0/description" && s.LocalizationKey == "postActions/pa0/description" && s.Value == "pa0_desc");
            Assert.Contains(strings, s => s.Identifier == "//postactions/1/description" && s.LocalizationKey == "postActions/pa1/description" && s.Value == "pa1_desc");
        }

        [Fact]
        public void ManualInstructionsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(strings, s => s.Identifier == "//postactions/0/manualinstructions/0/text" && s.LocalizationKey == "postActions/pa0/manualInstructions/first_instruction/text" && s.Value == "pa0_manualInstructions");
            Assert.Contains(strings, s => s.Identifier == "//postactions/2/manualinstructions/0/text" && s.LocalizationKey == "postActions/pa2/manualInstructions/default/text" && s.Value == "pa2_manualInstructions");
        }

        private static IReadOnlyList<TemplateString> ExtractStrings(string json, out string language)
        {
            JsonDocument jsonDocument = JsonDocument.Parse(json, new JsonDocumentOptions()
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            TemplateStringExtractor templateStringExtractor = new(jsonDocument);
            return templateStringExtractor.ExtractStrings(out language);
        }

        private static string GetTestTemplateJsonContent()
        {
            string thisDir = Path.GetDirectoryName(typeof(StringExtractorTests).Assembly.Location)
                ?? throw new Exception("Failed to get assembly location, which is required to access test templates.");
            string templateJsonPath = Path.GetFullPath(Path.Combine(
                thisDir,
                "..",
                "..",
                "..",
                "..",
                "..",
                "test",
                "Microsoft.TemplateEngine.TestTemplates",
                "test_templates",
                "TemplateWithLocalization",
                ".template.config",
                "template.json"));

            return File.ReadAllText(templateJsonPath);
        }
    }
}
