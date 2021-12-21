// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Template information, used to be stored in the template cache.
    /// This information is common for all templates that can be managed by different <see cref="IGenerator"/>s.
    /// </summary>
    public interface ITemplateInfo
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
        /// Gets generator ID the template should be processed by.
        /// </summary>
        Guid GeneratorId { get; }

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

        [Obsolete("Templates support multiple short names, use ShortNameList instead")]
        string ShortName { get; }

        [Obsolete("For choice parameters, use Parameters instead. For template tags, use IReadOnlyDictionary<string, string> TagsCollection instead.")]
        IReadOnlyDictionary<string, ICacheTag> Tags { get; }

        /// <summary>
        /// Gets template tags.
        /// </summary>
        IReadOnlyDictionary<string, string> TagsCollection { get; }

        [Obsolete("Use Parameters instead.")]
        IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; }

        /// <summary>
        /// Gets the list of template parameters.
        /// </summary>
        IReadOnlyList<ITemplateParameter> Parameters { get; }

        /// <summary>
        /// Gets template mount point URI.
        /// </summary>
        string MountPointUri { get; }

        /// <summary>
        /// Gets location of the configuration.
        /// </summary>
        string ConfigPlace { get; }

        /// <summary>
        /// Gets location of the localization configuration.
        /// </summary>
        string? LocaleConfigPlace { get; }

        /// <summary>
        /// Gets location of the host configuration.
        /// </summary>
        string? HostConfigPlace { get; }

        /// <summary>
        /// Gets third party notices.
        /// </summary>
        string? ThirdPartyNotices { get; }

        /// <summary>
        /// Gets the collection of baselines defined in the template.
        /// </summary>
        IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; }

        [Obsolete("This property is obsolete.")]
        bool HasScriptRunningPostActions { get; set; }

        /// <summary>
        /// Gets the list of short names defined for the template.
        /// </summary>
        IReadOnlyList<string> ShortNameList { get; }

        /// <summary>
        /// Gets the list of post actions IDs defined in the template.
        /// </summary>
        IReadOnlyList<Guid> PostActions { get;  }
    }
}
