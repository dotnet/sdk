// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    internal class PostActionLocalizationModel : IPostActionLocalizationModel
    {
        /// <summary>
        /// Localized description of the post action.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the localized manual instructions of the post action.
        /// The keys represent the manual instruction ids.
        /// </summary>
        public IReadOnlyDictionary<string, string> Instructions { get; set; } = new Dictionary<string, string>();
    }
}
