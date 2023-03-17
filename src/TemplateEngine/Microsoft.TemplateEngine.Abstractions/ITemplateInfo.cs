// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Template information, used to be stored in the template cache.
    /// This information is common for all templates that can be managed by different <see cref="IGenerator"/>s.
    /// </summary>
    public interface ITemplateInfo : ITemplateMetadata, IExtendedTemplateLocator
    {
        //all members are coming from ITemplateMetadata and IExtendedTemplateLocator

        [Obsolete("Templates support multiple short names, use ShortNameList instead")]
        string ShortName { get; }

        [Obsolete("For choice parameters, use ParameterDefinitionSet instead. For template tags, use IReadOnlyDictionary<string, string> TagsCollection instead.")]
        IReadOnlyDictionary<string, ICacheTag> Tags { get; }

        [Obsolete("Use ParameterDefinitionSet instead.")]
        IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; }

        /// <summary>
        /// Gets the list of template parameters.
        /// </summary>
        [Obsolete("Use ParameterDefinitionSet instead.", false)]
        IReadOnlyList<ITemplateParameter> Parameters { get; }

        [Obsolete("This property is obsolete.")]
        bool HasScriptRunningPostActions { get; set; }
    }
}
