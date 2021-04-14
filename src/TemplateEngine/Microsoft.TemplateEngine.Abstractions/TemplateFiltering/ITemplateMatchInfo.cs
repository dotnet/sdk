// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.TemplateFiltering
{
    /// <summary>
    /// Template with information about matching the filters.
    /// </summary>
    public interface ITemplateMatchInfo
    {
        /// <summary>
        /// True when the template matched all the filters applied.
        /// </summary>
        bool IsMatch { get; }

        /// <summary>
        /// True when the template matched at least one of the filters applied.
        /// </summary>
        bool IsPartialMatch { get; }

        /// <summary>
        /// Gets the template the filters applied to.
        /// </summary>
        ITemplateInfo Info { get; }

        /// <summary>
        /// Gets match information for the filters applied to template.
        /// </summary>
        IReadOnlyList<MatchInfo> MatchDisposition { get; }

        /// <summary>
        /// Adds the match information.
        /// </summary>
        /// <param name="newDisposition"></param>
        void AddMatchDisposition(MatchInfo newDisposition);
    }
}
