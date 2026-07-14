// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using System.Text.Json.Nodes;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    public static class CliHostSearchCacheData
    {
        public const string DataName = "cliHostData";
        private static readonly string[] _hostDataPropertyNames = new[] { "isHidden", "SymbolInfo", "UsageExamples" };

        public static Func<object, object> Reader => (obj) =>
        {
            JsonObject? cacheObject = obj as JsonObject;
            if (cacheObject == null)
            {
                return HostSpecificTemplateData.Default;
            }
            try
            {
                if (cacheObject.Count == 0)
                {
                    return HostSpecificTemplateData.Default;
                }

                var keys = new HashSet<string>(cacheObject.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);
                if (_hostDataPropertyNames.Any(keys.Contains))
                {
                    return new HostSpecificTemplateData(cacheObject);
                }

                //fallback to old behavior
                Dictionary<string, HostSpecificTemplateData> cliData = new();
                foreach (KeyValuePair<string, JsonNode?> data in cacheObject)
                {
                    try
                    {
                        cliData[data.Key] = new HostSpecificTemplateData(data.Value as JsonObject);
                    }
                    catch (Exception ex)
                    {
                        Reporter.Verbose.WriteLine($"Error deserializing the cli host specific template data for template {data.Key}, details:{ex}");
                    }
                }
                return cliData;
            }
            catch (Exception ex)
            {
                Reporter.Verbose.WriteLine($"Error deserializing the cli host specific template data {cacheObject}, details:{ex}");
            }
            return HostSpecificTemplateData.Default;
        };
    }
}
