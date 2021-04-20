// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    public interface ISearchCacheConfig
    {
        string TemplateDiscoveryFileName { get; }

        IReadOnlyDictionary<string, Func<JObject, object>> AdditionalDataReaders { get; }
    }
}
