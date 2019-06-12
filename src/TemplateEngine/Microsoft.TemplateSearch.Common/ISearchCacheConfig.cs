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
