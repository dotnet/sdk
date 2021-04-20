// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplateDiscoveryMetadata
    {
        public TemplateDiscoveryMetadata(string version, IReadOnlyList<ITemplateInfo> templateCache, IReadOnlyDictionary<string, PackToTemplateEntry> packToTemplateMap, IReadOnlyDictionary<string, object> additionalData)
        {
            Version = version;
            TemplateCache = templateCache;
            PackToTemplateMap = packToTemplateMap;
            AdditionalData = additionalData;
        }

        public string Version { get; }

        public IReadOnlyList<ITemplateInfo> TemplateCache { get; }

        public IReadOnlyDictionary<string, PackToTemplateEntry> PackToTemplateMap { get; }

        public IReadOnlyDictionary<string, object> AdditionalData { get; }
    }
}
