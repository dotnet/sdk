// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    public class PostActionLocalizationModel : IPostActionLocalizationModel
    {
        /// <summary>
        /// The key used in JSON to declare manual instructions.
        /// </summary>
        private const string _manualInstructionsKey = "manualInstructions";

        /// <summary>
        /// Identifier for the post action as declared in the culture-neutral template config file.
        /// </summary>
        public Guid ActionId { get; set; }

        /// <summary>
        /// Localized description of the post action.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Contains the localized manual instructions of the post action.
        /// Order of the items here matches the order of the manual instructions defined
        /// in the culture-neutral template config file.
        /// </summary>
        /// <returns>
        /// The list of localized instructions. A null value means that a localization
        /// was not provided for that instruction and the culture-neutral text should be used.
        /// </returns>
        public IReadOnlyList<string?> Instructions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Creates an object containing the localizations of a post action from the given json object.
        /// </summary>
        /// <param name="postActionSection">Json object, containig the localizations of a post action.</param>
        public static PostActionLocalizationModel FromJObject(JObject postActionSection)
        {
            return new PostActionLocalizationModel()
            {
                ActionId = postActionSection.ToGuid(nameof(ActionId)),
                Description = postActionSection.ToString(nameof(Description)),
                Instructions = GetLocalizedInstructions(postActionSection)
            };
        }

        private static IReadOnlyList<string?> GetLocalizedInstructions(JObject postActionSection)
        {
            JArray? instructionArray = postActionSection.Get<JToken>(_manualInstructionsKey) as JArray;
            if (instructionArray == null)
            {
                return Array.Empty<string?>();
            }

            List<string?> manualInstructions = new List<string?>();
            foreach (var instruction in instructionArray)
            {
                if (instruction == null)
                {
                    continue;
                }

                if (instruction.Type != JTokenType.Object)
                {
                    // TODO Log: Manual instructions should consist of objects.
                    continue;
                }

                manualInstructions.Add(instruction.ToString("text"));
            }

            return manualInstructions;
        }
    }
}
