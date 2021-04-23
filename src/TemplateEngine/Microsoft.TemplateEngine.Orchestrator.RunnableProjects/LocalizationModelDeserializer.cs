// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal static class LocalizationModelDeserializer
    {
        private const string PostActionIndexPrefix = "postActions[";
        private const string PostActionIndexSuffix = "]";

        private const string ManualInstructionIndexPrefix = "manualInstructions[";
        private const string ManualInstructionIndexSuffix = "]";

        public static ILocalizationModel Deserialize(JObject data)
        {
            var locModel = new LocalizationModel();
            var parameterLocalizations = new Dictionary<string, ParameterSymbolLocalizationModel>();

            // Property names are in format: symbols.framework.choices.0.description
            // Split them using '.' and store together with the localized string (property value).
            IEnumerable<(IEnumerable<string> nameParts, string localizedString)> stringsWithNames = data.Properties()
                .Where(p => p.Value.Type == JTokenType.String)
                .Select(p => (p.Name.Split('.').AsEnumerable(), p.Value.ToString()))
                .ToList();

            var symbols = LoadSymbolModels(stringsWithNames
                .Where(s => s.nameParts.FirstOrDefault() == "symbols")
                .Select(s => (s.nameParts.Skip(1), s.localizedString)));

            var postActions = LoadPostActionModels(stringsWithNames
                .Where(s => s.nameParts.FirstOrDefault().StartsWith(PostActionIndexPrefix))
                .Select(s => (s.nameParts, s.localizedString)));

            return new LocalizationModel()
            {
                Author = stringsWithNames.FirstOrDefault(s => s.nameParts.SingleOrDefault() == "author").localizedString,
                Name = stringsWithNames.FirstOrDefault(s => s.nameParts.SingleOrDefault() == "name").localizedString,
                Description = stringsWithNames.FirstOrDefault(s => s.nameParts.SingleOrDefault() == "description").localizedString,
                ParameterSymbols = symbols,
                // TODO uncomment once type compatibility issues are fixed.
                // PostActions = postActions,
            };
        }

        /// <summary>
        /// Generates parameter symbol localization models. The given name parts should begin with the parameter name
        /// as shown below and should not contain the string "symbols".
        /// <list type="table">
        /// <item>framework.displayName</item>
        /// <item>framework.description</item>
        /// <item>framework.choices.net5_0.description</item>
        /// <item>targetframeworkoverride.description</item>
        /// </list>
        /// </summary>
        private static IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> LoadSymbolModels(IEnumerable<(IEnumerable<string> nameParts, string localizedString)> strings)
        {
            var results = new Dictionary<string, IParameterSymbolLocalizationModel>();

            // Group by symbol name
            foreach (var parameterParts in strings.GroupBy(p => p.nameParts.FirstOrDefault()))
            {
                if (string.IsNullOrEmpty(parameterParts.Key))
                {
                    // Symbol with no name. Ignore.
                    continue;
                }

                string symbolName = parameterParts.Key;
                string? displayName = parameterParts.FirstOrDefault(p => p.nameParts.Skip(1).SingleOrDefault() == "displayName").localizedString;
                string? description = parameterParts.FirstOrDefault(p => p.nameParts.Skip(1).SingleOrDefault() == "description").localizedString;

                IReadOnlyDictionary<string, ParameterChoiceLocalizationModel>? choiceModels = LoadChoiceModels(strings
                    .Where(s => s.nameParts.Skip(1).FirstOrDefault() == "choices")
                    .Select(s => (s.nameParts.Skip(2), s.localizedString)));

                ParameterSymbolLocalizationModel paramLoc = new ParameterSymbolLocalizationModel(
                    symbolName,
                    displayName,
                    description,
                    choiceModels);

                results[symbolName] = paramLoc;
            }

            return results;
        }

        /// <summary>
        /// Generates post action localization models. The given parts should begin with the choice name
        /// as shown below (prior parts of the name such as "symbols" and parameter name shouldn't be included).
        /// <list type="table">
        /// <item>net5_0.displayName</item>
        /// <item>net5_0.description</item>
        /// <item>netstandard2_0.description</item>
        /// </list>
        /// </summary>
        private static IReadOnlyDictionary<string, ParameterChoiceLocalizationModel> LoadChoiceModels(IEnumerable<(IEnumerable<string> nameParts, string localizedString)> strings)
        {
            var results = new Dictionary<string, ParameterChoiceLocalizationModel>();

            foreach (var choiceParts in strings.GroupBy(p => p.nameParts.FirstOrDefault()))
            {
                if (string.IsNullOrEmpty(choiceParts.Key))
                {
                    // Choice with no name. Ignore
                    continue;
                }

                string? displayName = choiceParts.FirstOrDefault(p => p.nameParts.Skip(1).SingleOrDefault() == "displayName").localizedString;
                string? description = choiceParts.FirstOrDefault(p => p.nameParts.Skip(1).SingleOrDefault() == "description").localizedString;

                results.Add(choiceParts.Key, new ParameterChoiceLocalizationModel(displayName, description));
            }

            return results;
        }

        /// <summary>
        /// Generates post action localization models from name parts such as:
        /// <list type="table">
        /// <item>postActions[0].description</item>
        /// <item>postActions[0].manualInstructions[0].text</item>
        /// <item>postActions[0].manualInstructions[1].text</item>
        /// </list>
        /// </summary>
        private static IReadOnlyList<IPostActionLocalizationModel?> LoadPostActionModels(IEnumerable<(IEnumerable<string> nameParts, string localizedString)> strings)
        {
            var results = new List<IPostActionLocalizationModel?>();

            foreach (var postActionParts in strings.GroupBy(p => p.nameParts.FirstOrDefault()))
            {
                if (!GetIndexFromString(postActionParts.Key, PostActionIndexPrefix, PostActionIndexSuffix, out int postActionIndex) ||
                    postActionIndex > 256)
                {
                    // Invalid index.
                    continue;
                }

                while (results.Count <= postActionIndex)
                {
                    // We haven't processed localizations for these post actions. Just put null for now.
                    // Localization file also may not specify translations for all the post actions.
                    results.Add(null);
                }

                string? description = postActionParts.FirstOrDefault(p => p.nameParts.Skip(1).SingleOrDefault() == "description").localizedString;
                var instructions = LoadManualInstructionModels(postActionParts
                    .Where(s => s.nameParts.Skip(1).FirstOrDefault().StartsWith(ManualInstructionIndexPrefix))
                    .Select(s => (s.nameParts.Skip(1), s.localizedString)));

                results[postActionIndex] = new PostActionLocalizationModel()
                {
                    Description = description,
                    Instructions = instructions,
                };
            }

            return results;
        }

        /// <summary>
        /// Generates manual instruction localization models. The given parts should begin with the index string
        /// as shown below and shouldn't include "postActions[x]".
        /// <list type="table">
        /// <item>manualInstructions[0].text</item>
        /// <item>manualInstructions[1].text</item>
        /// </list>
        /// </summary>
        private static IReadOnlyList<string?> LoadManualInstructionModels(IEnumerable<(IEnumerable<string> nameParts, string localizedString)> strings)
        {
            var results = new List<string?>();

            foreach (var instructionParts in strings.GroupBy(p => p.nameParts.FirstOrDefault()))
            {
                if (!GetIndexFromString(instructionParts.Key, ManualInstructionIndexPrefix, ManualInstructionIndexSuffix, out int instructionIndex) ||
                    instructionIndex > 256)
                {
                    // Invalid index.
                    continue;
                }

                while (results.Count <= instructionIndex)
                {
                    // We haven't processed localizations for these instructions. Just put null for now.
                    // Localization file also may not specify translations for all the manual instructions.
                    results.Add(null);
                }

                string? text = instructionParts.FirstOrDefault(p => p.nameParts.Skip(1).SingleOrDefault() == "text").localizedString;
                results[instructionIndex] = text;
            }

            return results;
        }

        /// <summary>
        /// Parses the index from string of format &lt;prefix&gt;index&lt;suffix&gt;.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="prefix">The prefix that the string should begin with. Index starts after the prefix.</param>
        /// <param name="suffix">The suffix that the string should end with. Index ends with the suffix.</param>
        /// <returns>True if parsing succeeded. False if the string was incorrectly formatted or the index
        /// string cannot be converted to an integer.</returns>
        private static bool GetIndexFromString(string value, string prefix, string suffix, out int index)
        {
            index = 0;

            if (!value.StartsWith(prefix) || !value.EndsWith(suffix))
            {
                return false;
            }

            if (value.Length <= prefix.Length + suffix.Length)
            {
                return false;
            }

            string indexString = value.Substring(prefix.Length, value.Length - prefix.Length - suffix.Length);
            return int.TryParse(indexString, out index);
        }
    }
}
