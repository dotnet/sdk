// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.Common.Providers;

namespace Microsoft.TemplateSearch.Common
{
    public static class Components
    {
        public static IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> AllComponents { get; } =
            new (Type Type, IIdentifiedComponent Instance)[]
            {
                (typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory())
            };
    }
}
