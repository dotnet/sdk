using System;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Filters
{
    internal static class TemplateJsonExistencePackFilter
    {
        private static readonly string _FilterId = "Template.json existence";

        private static ITemplateEngineHost _host;
        private static EngineEnvironmentSettings _environemntSettings;

        static TemplateJsonExistencePackFilter()
        {
            _host = TemplateEngineHostHelper.CreateHost("filterHost");
            _environemntSettings = new EngineEnvironmentSettings(_host, x => new SettingsLoader(x));
        }

        public static Func<IDownloadedPackInfo, PreFilterResult> SetupPackFilter()
        {
            Func<IDownloadedPackInfo, PreFilterResult> filter = (packInfo) =>
            {
                foreach (IMountPointFactory factory in _environemntSettings.SettingsLoader.Components.OfType<IMountPointFactory>())
                {
                    if (factory.TryMount(_environemntSettings, null, packInfo.Path, out IMountPoint mountPoint))
                    {
                        bool hasTemplateJson = mountPoint.Root.EnumerateFiles("template.json", SearchOption.AllDirectories).Any();
                        _environemntSettings.SettingsLoader.ReleaseMountPoint(mountPoint);

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
