// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.Common
{
    [Obsolete("The class is deprecated. Use TemplateSearchCache instead to create search cache data.")]
    internal class TemplateIdentificationEntry
    {
        internal TemplateIdentificationEntry(string identity, string? groupIdentity)
        {
            Identity = identity;
            GroupIdentity = groupIdentity;
        }

        [JsonProperty]
        internal string Identity { get; }

        [JsonProperty]
        internal string? GroupIdentity { get; }
    }
}
