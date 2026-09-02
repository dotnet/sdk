// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.TemplateEngine.TemplateLocalizer.Core.Exceptions;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.UnitTests
{
    [TestClass]
    public class StringExtractorTests
    {
        [TestMethod]
        public void FirstLevelStringsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(s => s.Identifier == "//name" && s.LocalizationKey == "name" && s.Value == "name", strings);
            Assert.Contains(s => s.Identifier == "//description" && s.LocalizationKey == "description" && s.Value == "desc", strings);
        }

        [TestMethod]
        public void CertainStringsAreOmitted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.DoesNotContain(s => s.Identifier == "//$schema" || s.LocalizationKey == "$schema", strings);
            Assert.DoesNotContain(s => s.Identifier == "//classification" || s.LocalizationKey == "classification", strings);
            Assert.DoesNotContain(s => s.Identifier == "//groupIdentity" || s.LocalizationKey == "groupIdentity", strings);
        }

        [TestMethod]
        public void DefaultAuthoringLanguageIsEnglish()
        {
            _ = ExtractStrings(GetTestTemplateJsonContent(), out string language);

            Assert.AreEqual("en", language);
        }

        [TestMethod]
        public void SymbolsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(s => s.Identifier == "//symbols/somesymbol/description" && s.LocalizationKey == "symbols/someSymbol/description" && s.Value == "sym0_desc", strings);
            Assert.Contains(s => s.Identifier == "//symbols/somesymbol/displayname" && s.LocalizationKey == "symbols/someSymbol/displayName" && s.Value == "sym0_displayName", strings);
            Assert.Contains(s => s.Identifier == "//symbols/somechoice/description" && s.LocalizationKey == "symbols/someChoice/description" && s.Value == "sym1_desc", strings);
            Assert.Contains(s => s.Identifier == "//symbols/somechoice/displayname" && s.LocalizationKey == "symbols/someChoice/displayName" && s.Value == "sym1_displayName", strings);
        }

        [TestMethod]
        public void SymbolChoicesAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(s => s.Identifier == "//symbols/somechoice/choices/0/description" && s.LocalizationKey == "symbols/someChoice/choices/choice0/description" && s.Value == "sym1_choice0", strings);
            Assert.Contains(s => s.Identifier == "//symbols/somechoice/choices/0/displayname" && s.LocalizationKey == "symbols/someChoice/choices/choice0/displayName" && s.Value == "sym1_choice0_displayName", strings);
            Assert.Contains(s => s.Identifier == "//symbols/somechoice/choices/2/description" && s.LocalizationKey == "symbols/someChoice/choices/choice2/description" && s.Value == "sym1_choice2", strings);
            Assert.Contains(s => s.Identifier == "//symbols/somechoice/choices/2/displayname" && s.LocalizationKey == "symbols/someChoice/choices/choice2/displayName" && s.Value == "sym1_choice2_displayName", strings);
        }

        [TestMethod]
        public void PostActionsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(s => s.Identifier == "//postactions/0/description" && s.LocalizationKey == "postActions/pa0/description" && s.Value == "pa0_desc", strings);
            Assert.Contains(s => s.Identifier == "//postactions/1/description" && s.LocalizationKey == "postActions/pa1/description" && s.Value == "pa1_desc", strings);
        }

        [TestMethod]
        public void ManualInstructionsAreExtracted()
        {
            var strings = ExtractStrings(GetTestTemplateJsonContent(), out _);

            Assert.Contains(s => s.Identifier == "//postactions/0/manualinstructions/0/text" && s.LocalizationKey == "postActions/pa0/manualInstructions/first_instruction/text" && s.Value == "pa0_manualInstructions", strings);
            Assert.Contains(s => s.Identifier == "//postactions/2/manualinstructions/0/text" && s.LocalizationKey == "postActions/pa2/manualInstructions/default/text" && s.Value == "pa2_manualInstructions", strings);
        }

        [TestMethod]
        public void PostActionsShouldHaveIds()
        {
            string json = @"{
    ""postActions"": [
        {
        },
    ]
}";
            var ex = Assert.ThrowsExactly<JsonMemberMissingException>(() => ExtractStrings(json, out _));
            Assert.Contains("postActions", ex.Message);
            Assert.Contains("id", ex.Message);
        }

        [TestMethod]
        public void PostActionIdsAreUnique()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postAction1""
        },
        {
            ""id"": ""postAction1""
        }
    ]
}";
            var ex = Assert.ThrowsExactly<LocalizationKeyIsNotUniqueException>(() => ExtractStrings(json, out _));
            Assert.Contains("postAction1", ex.Message);
            Assert.Contains("postActions", ex.Message);
        }

        [TestMethod]
        public void SingleManualInstructionDoesntNeedId()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postActionId"",
            ""manualInstructions"": [
                {
                    ""text"": ""some text""
                }
            ]
        }
    ]
}";
            var results = ExtractStrings(json, out _);
            Assert.Contains(r => r.LocalizationKey == "postActions/postActionId/manualInstructions/default/text", results);
        }

        [TestMethod]
        public void MultipleManualInstructionShouldHaveIds()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postActionId"",
            ""manualInstructions"": [
                {
                    ""text"": ""some text""
                },
                {
                    ""text"": ""some other text""
                }
            ]
        }
    ]
}";
            var ex = Assert.ThrowsExactly<JsonMemberMissingException>(() => ExtractStrings(json, out _));
            Assert.Contains("id", ex.Message);
            Assert.Contains("manualInstructions", ex.Message);
        }

        [TestMethod]
        public void ManualInstructionIdsAreUnique()
        {
            string json = @"{
    ""postActions"": [
        {
            ""id"": ""postActionId"",
            ""manualInstructions"": [
                {
                    ""id"": ""mi""
                },
                {
                    ""id"": ""mi""
                },
            ]
        }
    ]
}";
            var ex = Assert.ThrowsExactly<LocalizationKeyIsNotUniqueException>(() => ExtractStrings(json, out _));
            Assert.Contains("mi", ex.Message);
            Assert.Contains("manualInstructions", ex.Message);
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
            string templateJsonPath = Path.Combine(
                TestBase.TestTemplatesLocation,
                "TemplateWithLocalization",
                ".template.config",
                "template.json");

            return File.ReadAllText(templateJsonPath);
        }
    }
}
