using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.Nuget;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    public static class PackCheckResultReportWriter
    {
        public static readonly string CacheContentDirectory = "SearchCache";
        // All the metadata needed for searching from dotnet new.
        public static readonly string SearchMetadataFilename = "templateSearchInfo.json";
        // Metadata for the scraper to skip packs known to not contain templates.
        public static readonly string NonTemplatePacksFileName = "nonTemplatePacks.json";

        public static bool TryWriteResults(string outputBasePath, PackSourceCheckResult packSourceCheckResults)
        {
            try
            {
                string reportPath = Path.Combine(outputBasePath, CacheContentDirectory);

                if (!Directory.Exists(reportPath))
                {
                    Directory.CreateDirectory(reportPath);
                    Console.WriteLine($"Created directory:{reportPath}");
                }

                return TryWriteSearchMetadata(packSourceCheckResults, reportPath)
                    && TryWriteNonTemplatePackList(reportPath, packSourceCheckResults.PackCheckData);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryWriteSearchMetadata(PackSourceCheckResult packSourceCheckResults, string reportPath)
        {
            try
            {
                TemplateDiscoveryMetadata searchMetadata = CreateSearchMetadata(packSourceCheckResults);

                JObject toSerialize = JObject.FromObject(searchMetadata);
                string outputFileName = Path.Combine(reportPath, SearchMetadataFilename);
                File.WriteAllText(outputFileName, toSerialize.ToString());
                Console.WriteLine($"Search cache file created: {outputFileName}");

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static TemplateDiscoveryMetadata CreateSearchMetadata(PackSourceCheckResult packSourceCheckResults)
        {
            List<ITemplateInfo> templateCache = packSourceCheckResults.PackCheckData.Where(r => r.AnyTemplates)
                                                    .SelectMany(r => r.FoundTemplates)
                                                    .Distinct(new TemplateIdentityEqualityComparer())
                                                    .ToList();

            Dictionary<string, PackToTemplateEntry> packToTemplateMap = packSourceCheckResults.PackCheckData
                            .Where(r => r.AnyTemplates)
                            .ToDictionary(r => r.PackInfo.Id,
                                        r =>
                                        {
                                            PackToTemplateEntry packToTemplateEntry = new PackToTemplateEntry(
                                                    r.PackInfo.Version,
                                                    r.FoundTemplates.Select(t => new TemplateIdentificationEntry(t.Identity, t.GroupIdentity)).ToList());

                                            if (r.PackInfo is NugetPackInfo npi)
                                            {
                                                packToTemplateEntry.TotalDownloads = npi.TotalDownloads;
                                            }
                                            return packToTemplateEntry;
                                        });


            Dictionary<string, object> additionalData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (IAdditionalDataProducer dataProducer in packSourceCheckResults.AdditionalDataProducers)
            {
                additionalData[dataProducer.DataUniqueName] = dataProducer.Data;
            }

            return new TemplateDiscoveryMetadata(SettingsStore.CurrentVersion, templateCache, packToTemplateMap, additionalData);
        }

        private class TemplateIdentityEqualityComparer : IEqualityComparer<ITemplateInfo>
        {
            public bool Equals(ITemplateInfo x, ITemplateInfo y)
            {
                return string.Equals(x.Identity, y.Identity, System.StringComparison.Ordinal);
            }

            public int GetHashCode(ITemplateInfo obj)
            {
                return obj.Identity.GetHashCode();
            }
        }

        private static bool TryWriteNonTemplatePackList(string reportPath, IReadOnlyList<PackCheckResult> packCheckResults)
        {
            try
            {
                List<string> packsWithoutTemplates = packCheckResults.Where(r => !r.AnyTemplates)
                                                                    .Select(r => r.PackInfo.Id)
                                                                    .ToList();
                string serializedContent = JsonConvert.SerializeObject(packsWithoutTemplates, Formatting.Indented);

                string outputFileName = Path.Combine(reportPath, NonTemplatePacksFileName);
                File.WriteAllText(outputFileName, serializedContent);
                Console.WriteLine($"Non template pack list was created: {outputFileName}");
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
