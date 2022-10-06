// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Common template metadata.
    /// </summary>
    public interface ITemplateMetadata
    {
        /// <summary>
        /// Gets template author.
        /// </summary>
        string? Author { get; }

        /// <summary>
        /// Gets template description.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets template classifications.
        /// </summary>
        /// <example>"Common", "Library", "Windows", "Tests", "Web".</example>
        IReadOnlyList<string> Classifications { get; }

        /// <summary>
        /// Gets default name.
        /// </summary>
        /// <remarks>
        /// It is used as source name, in case name and fallback name passed to TemplateCreator is null.
        /// </remarks>
        string? DefaultName { get; }

        /// <summary>
        /// Gets template identity.
        /// </summary>
        string Identity { get; }

        /// <summary>
        /// Gets template group identity.
        /// </summary>
        string? GroupIdentity { get; }

        /// <summary>
        /// Gets template precedence.
        /// The precedence is used to prioritized templates inside template groups: the template with the higher value will be prioritized over the template with lower value.
        /// </summary>
        int Precedence { get; }

        /// <summary>
        /// Gets template name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets template tags.
        /// </summary>
        IReadOnlyDictionary<string, string> TagsCollection { get; }

        /// <summary>
        /// Gets the list of template parameters.
        /// </summary>
        IParameterDefinitionSet ParameterDefinitions { get; }

        /// <summary>
        /// Gets third party notices.
        /// </summary>
        string? ThirdPartyNotices { get; }

        /// <summary>
        /// Gets the collection of baselines defined in the template.
        /// </summary>
        IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; }

        /// <summary>
        /// Gets the list of short names defined for the template.
        /// </summary>
        IReadOnlyList<string> ShortNameList { get; }

        /// <summary>
        /// Gets the list of post actions IDs defined in the template.
        /// </summary>
        IReadOnlyList<Guid> PostActions { get; }

        /// <summary>
        /// Gets the information about constraints defined in the template.
        /// </summary>
        IReadOnlyList<TemplateConstraintInfo> Constraints { get; }

        /// <summary>
        /// Gets template's preference for using the default name on creation.
        /// </summary>
        bool PreferDefaultName { get; }
    }
}
