// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal static class LocalizationModelDeserializer
    {
        /// <summary>
        /// Character to be used when separating a key into parts.
        /// </summary>
        private const char KeySeparator = '/';

        private const string PostActionIndexPrefix = "postActions[";
        private const string PostActionIndexSuffix = "]";

        private const string ManualInstructionIndexPrefix = "manualInstructions[";
        private const string ManualInstructionIndexSuffix = "]";

        public static ILocalizationModel Deserialize(JObject data)
        {
            var parameterLocalizations = new Dictionary<string, ParameterSymbolLocalizationModel>();

            List<(string Key, string Value)> localizedStrings = data.Properties()
                .Select(p => p.Value.Type == JTokenType.String ? (p.Name, p.Value.ToString()) : throw new Exception(LocalizableStrings.Authoring_InvalidJsonElementInLocalizationFile))
                .ToList();

            var symbols = LoadSymbolModels(localizedStrings);
            var postActions = LoadPostActionModels(localizedStrings);

            return new LocalizationModel(
                name: localizedStrings.FirstOrDefault(s => s.Key == "name").Value,
                description: localizedStrings.FirstOrDefault(s => s.Key == "description").Value,
                author: localizedStrings.FirstOrDefault(s => s.Key == "author").Value,
                symbols,
                postActions);
        }

        /// <summary>
        /// Generates parameter symbol localization models from the given localized strings.
        /// </summary>
        private static IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> LoadSymbolModels(List<(string Key, string Value)> localizedStrings)
        {
            var results = new Dictionary<string, IParameterSymbolLocalizationModel>();

            // Property names are in format: symbols/framework/choices[0]/description
            // Split them using '/' and store together with the localized string.
            IEnumerable<(IEnumerable<string> NameParts, string LocalizedString)> strings = localizedStrings
                .Where(s => s.Key.StartsWith("symbols" + KeySeparator))
                .Select(s => (s.Key.Split(KeySeparator).AsEnumerable().Skip(1), s.Value))
                .ToList();

            // Group by symbol name
            foreach (var parameterParts in strings.GroupBy(p => p.NameParts.FirstOrDefault()))
            {
                if (string.IsNullOrEmpty(parameterParts.Key))
                {
                    // Symbol with no name. Ignore.
                    continue;
                }

                string symbolName = parameterParts.Key;
                string? displayName = parameterParts.SingleOrDefault(p => p.NameParts.Skip(1).FirstOrDefault() == "displayName").LocalizedString;
                string? description = parameterParts.SingleOrDefault(p => p.NameParts.Skip(1).FirstOrDefault() == "description").LocalizedString;

                IReadOnlyDictionary<string, ParameterChoiceLocalizationModel>? choiceModels = LoadChoiceModels(strings
                    .Where(s => s.NameParts.Skip(1).FirstOrDefault() == "choices")
                    .Select(s => (s.NameParts.Skip(2), s.LocalizedString)));

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
        /// <item>net5.0/displayName</item>
        /// <item>net5.0/description</item>
        /// <item>netstandard2.0/description</item>
        /// </list>
        /// </summary>
        private static IReadOnlyDictionary<string, ParameterChoiceLocalizationModel> LoadChoiceModels(IEnumerable<(IEnumerable<string> NameParts, string LocalizedString)> strings)
        {
            var results = new Dictionary<string, ParameterChoiceLocalizationModel>();

            foreach (var choiceParts in strings.GroupBy(p => p.NameParts.FirstOrDefault()))
            {
                if (string.IsNullOrEmpty(choiceParts.Key))
                {
                    // Choice with no name. Ignore
                    continue;
                }

                string? displayName = choiceParts.SingleOrDefault(p => p.NameParts.Skip(1).FirstOrDefault() == "displayName").LocalizedString;
                string? description = choiceParts.SingleOrDefault(p => p.NameParts.Skip(1).FirstOrDefault() == "description").LocalizedString;

                results.Add(choiceParts.Key, new ParameterChoiceLocalizationModel(displayName, description));
            }

            return results;
        }

        /// <summary>
        /// Generates post action localization models from the given localized strings.
        /// </summary>
        private static IReadOnlyDictionary<int, IPostActionLocalizationModel> LoadPostActionModels(List<(string Key, string Value)> localizedStrings)
        {
            var results = new Dictionary<int, IPostActionLocalizationModel>();

            // Property names are in format: postActions[2]/manualInstructions[0]/description
            // Split them using '/' and store together with the localized string.
            IEnumerable<(IEnumerable<string> NameParts, string LocalizedString)> strings = localizedStrings
                .Where(s => s.Key.StartsWith(PostActionIndexPrefix))
                .Select(s => (s.Key.Split(KeySeparator).AsEnumerable(), s.Value))
                .ToList();

            foreach (var postActionParts in strings.GroupBy(p => p.NameParts.FirstOrDefault()))
            {
                if (!GetIndexFromString(postActionParts.Key, PostActionIndexPrefix, PostActionIndexSuffix, out int postActionIndex))
                {
                    // Invalid index.
                    continue;
                }

                string? description = postActionParts.SingleOrDefault(p => p.NameParts.Skip(1).FirstOrDefault() == "description").LocalizedString;
                var instructions = LoadManualInstructionModels(postActionParts
                    .Where(s => s.NameParts.Skip(1).FirstOrDefault().StartsWith(ManualInstructionIndexPrefix))
                    .Select(s => (s.NameParts.Skip(1), s.LocalizedString)));

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
        /// <item>manualInstructions[0]/text</item>
        /// <item>manualInstructions[1]/text</item>
        /// </list>
        /// </summary>
        private static IReadOnlyDictionary<int, string> LoadManualInstructionModels(IEnumerable<(IEnumerable<string> NameParts, string LocalizedString)> strings)
        {
            var results = new Dictionary<int, string>();

            foreach (var instructionParts in strings.GroupBy(p => p.NameParts.FirstOrDefault()))
            {
                if (!GetIndexFromString(instructionParts.Key, ManualInstructionIndexPrefix, ManualInstructionIndexSuffix, out int instructionIndex))
                {
                    // Invalid index.
                    continue;
                }

                string? text = instructionParts.SingleOrDefault(p => p.NameParts.Skip(1).FirstOrDefault() == "text").LocalizedString;
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
