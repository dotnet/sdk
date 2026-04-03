// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    [Obsolete("The class is deprecated. Use TemplateSearchCache instead to create search cache data.")]
    internal class TemplateDiscoveryMetadata
    {
        internal TemplateDiscoveryMetadata(string version, IReadOnlyList<ITemplateInfo> templateCache, IReadOnlyDictionary<string, PackToTemplateEntry> packToTemplateMap, IReadOnlyDictionary<string, object> additionalData)
        {
            Version = version;
            TemplateCache = templateCache;
            PackToTemplateMap = packToTemplateMap;
            AdditionalData = additionalData;
        }

        [JsonInclude]
        internal string Version { get; }

        [JsonInclude]
        internal IReadOnlyList<ITemplateInfo> TemplateCache { get; }

        [JsonInclude]
        internal IReadOnlyDictionary<string, PackToTemplateEntry> PackToTemplateMap { get; }

        [JsonInclude]
        internal IReadOnlyDictionary<string, object> AdditionalData { get; }

#if NET7_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "Serializes a known internal type (TemplateDiscoveryMetadata).")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Serializes a known internal type (TemplateDiscoveryMetadata).")]
#endif
        internal JsonObject ToJObject()
        {
            return JExtensions.FromObject(this);
        }
    }
}
