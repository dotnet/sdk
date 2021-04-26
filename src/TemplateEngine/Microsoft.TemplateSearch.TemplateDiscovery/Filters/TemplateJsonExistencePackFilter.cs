// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Filters
{
    internal static class TemplateJsonExistencePackFilter
    {
        private const string _FilterId = "Template.json existence";

        private static ITemplateEngineHost _host = TemplateEngineHostHelper.CreateHost("filterHost");

        public static Func<IDownloadedPackInfo, PreFilterResult> SetupPackFilter()
        {
            Func<IDownloadedPackInfo, PreFilterResult> filter = (packInfo) =>
            {
                using EngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(_host, virtualizeSettings: true);
                foreach (IMountPointFactory factory in environmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>())
                {
                    if (factory.TryMount(environmentSettings, null, packInfo.Path, out IMountPoint mountPoint))
                    {
                        bool hasTemplateJson = mountPoint.Root.EnumerateFiles("template.json", SearchOption.AllDirectories).Any();
                        mountPoint.Dispose();

                        if (hasTemplateJson)
                        {
                            return new PreFilterResult()
                            {
                                FilterId = _FilterId,
                                IsFiltered = false,
                                Reason = string.Empty
                            };
                        }

                        break;  // this factory mounted the pack. No more checking is needed.
                    }
                }

                return new PreFilterResult()
                {
                    FilterId = _FilterId,
                    IsFiltered = true,
                    Reason = "Package did not contain any template.json files"
                };
            };

            return filter;
        }
    }
}
