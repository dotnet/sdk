// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget;

internal class FilterNonMicrosoftAuthors
{
    private const string _FilterId = "Template.json contains Author=Microsoft";

    private static ITemplateEngineHost _host = TemplateEngineHostHelper.CreateHost("filterHost");

    internal static Func<IDownloadedPackInfo, PreFilterResult> SetupPackFilter()
    {
        Func<IDownloadedPackInfo, PreFilterResult> filter = (packInfo) =>
        {
            // All NuGet packages that start with Microsoft. are OK since that is protected prefix on NuGet.org
            if (packInfo.Name.StartsWith("Microsoft."))
            {
                return new PreFilterResult(_FilterId, isFiltered: false);
            }
            EngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(_host, virtualizeSettings: true);
            foreach (IMountPointFactory factory in environmentSettings.Components.OfType<IMountPointFactory>())
            {
                if (factory.TryMount(environmentSettings, null, packInfo.Path, out IMountPoint mountPoint))
                {
                    foreach (var templateJson in mountPoint.Root.EnumerateFiles("template.json", SearchOption.AllDirectories))
                    {
                        try
                        {
                            using (var streamReader = new StreamReader(templateJson.OpenRead()))
                            using (var jsonReader = new JsonTextReader(streamReader))
                            {
                                var jObject = JObject.Load(jsonReader);
                                var author = jObject["author"]?.Value<string>();
                                if (author?.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ?? false)
                                {
                                    return new PreFilterResult(_FilterId, isFiltered: true, $"{templateJson.FullPath} has Author=Microsoft and package id is {packInfo.Name}");
                                }
                            }
                        }
                        catch (Exception)
                        {
                            return new PreFilterResult(_FilterId, isFiltered: false);
                        }
                    }
                    mountPoint.Dispose();
                }
            }
            return new PreFilterResult(_FilterId, isFiltered: false);
        };

        return filter;
    }
}
