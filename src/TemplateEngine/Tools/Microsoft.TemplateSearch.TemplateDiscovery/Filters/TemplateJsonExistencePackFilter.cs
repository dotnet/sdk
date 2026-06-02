// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Filters
{
    internal static class TemplateJsonExistencePackFilter
    {
        private const string FilterId = "Template.json existence";

        private static readonly ITemplateEngineHost Host = TemplateEngineHostHelper.CreateHost("filterHost");

        internal static Func<IDownloadedPackInfo, PreFilterResult> SetupPackFilter()
        {
            static PreFilterResult Filter(IDownloadedPackInfo packInfo)
            {
                using EngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(Host, virtualizeSettings: true);
                foreach (IMountPointFactory factory in environmentSettings.Components.OfType<IMountPointFactory>())
                {
                    if (factory.TryMount(environmentSettings, null, packInfo.Path, out IMountPoint? mountPoint))
                    {
                        bool hasTemplateJson = mountPoint!.Root.EnumerateFiles("template.json", SearchOption.AllDirectories).Any();
                        mountPoint.Dispose();

                        if (hasTemplateJson)
                        {
                            return new PreFilterResult(FilterId, isFiltered: false);
                        }

                        break;  // this factory mounted the pack. No more checking is needed.
                    }
                }

                return new PreFilterResult(FilterId, isFiltered: true, "Package did not contain any template.json files");
            }

            return Filter;
        }
    }
}
