// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    internal class PostActionLocalizationModel : IPostActionLocalizationModel
    {
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
        public IReadOnlyDictionary<int, string> Instructions { get; set; } = new Dictionary<int, string>();
    }
}
