// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    public static class CliHostSearchCacheData
    {
        public const string DataName = "cliHostData";
        private static readonly string[] HostDataPropertyNames = new[] { "isHidden", "SymbolInfo", "UsageExamples" };

        public static Func<object, object> Reader => (obj) =>
        {
            if (obj is not JObject cacheObject)
            {
                return CliHostTemplateData.Default;
            }
            try
            {
                if (HostDataPropertyNames.Contains(cacheObject.Properties().First().Name, StringComparer.OrdinalIgnoreCase))
                {
                    return new CliHostTemplateData(cacheObject);
                }

                //fallback to old behavior
                Dictionary<string, CliHostTemplateData> cliData = new Dictionary<string, CliHostTemplateData>();
                foreach (JProperty data in cacheObject.Properties())
                {
                    try
                    {
                        cliData[data.Name] = new CliHostTemplateData(data.Value as JObject);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error deserializing the cli host specific template data for template {data.Name}, details:{ex}");
                    }
                }
                return cliData;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deserializing the cli host specific template data {cacheObject}, details:{ex}");
            }
            return CliHostTemplateData.Default;
        };
    }
}
