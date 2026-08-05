// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    public static class CliHostSearchCacheData
    {
        public const string DataName = "cliHostData";
        private static readonly string[] HostDataPropertyNames = new[] { "isHidden", "SymbolInfo", "UsageExamples" };

        public static Func<object, object> Reader => (obj) =>
        {
            if (obj is not JsonObject cacheObject)
            {
                return CliHostTemplateData.Default;
            }
            try
            {
                if (HostDataPropertyNames.Contains(cacheObject.First().Key, StringComparer.OrdinalIgnoreCase))
                {
                    return new CliHostTemplateData(cacheObject);
                }

                //fallback to old behavior
                Dictionary<string, CliHostTemplateData> cliData = new Dictionary<string, CliHostTemplateData>();
                foreach (var data in cacheObject)
                {
                    try
                    {
                        cliData[data.Key] = new CliHostTemplateData(data.Value as JsonObject);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error deserializing the cli host specific template data for template {data.Key}, details:{ex}");
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
