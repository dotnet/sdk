// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.Nuget;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    internal static class PackCheckResultReportWriter
    {
        internal const string CacheContentDirectory = "SearchCache";
        // All the metadata needed for searching from dotnet new.
        internal const string SearchMetadataFilename = "NuGetTemplateSearchInfo.json";
        // Metadata for the scraper to skip packs known to not contain templates.
        internal const string NonTemplatePacksFileName = "nonTemplatePacks.json";

        internal static string WriteResults(string outputBasePath, PackSourceCheckResult packSourceCheckResults)
        {
            string reportPath = Path.Combine(outputBasePath, CacheContentDirectory);

            if (!Directory.Exists(reportPath))
            {
                Directory.CreateDirectory(reportPath);
                Console.WriteLine($"Created directory:{reportPath}");
            }
            WriteNonTemplatePackList(reportPath, packSourceCheckResults.PackCheckData);
            return WriteSearchMetadata(packSourceCheckResults, reportPath);

        }

        private static string WriteSearchMetadata(PackSourceCheckResult packSourceCheckResults, string reportPath)
        {
            TemplateDiscoveryMetadata searchMetadata = CreateSearchMetadata(packSourceCheckResults);

            JObject toSerialize = JObject.FromObject(searchMetadata);
            string outputFileName = Path.Combine(reportPath, SearchMetadataFilename);
            File.WriteAllText(outputFileName, toSerialize.ToString());
            Console.WriteLine($"Search cache file created: {outputFileName}");
            return outputFileName;
        }

        private static TemplateDiscoveryMetadata CreateSearchMetadata(PackSourceCheckResult packSourceCheckResults)
        {
            List<ITemplateInfo> templateCache = packSourceCheckResults.PackCheckData.Where(r => r.AnyTemplates)
                                                    .SelectMany(r => r.FoundTemplates)
                                                    .Distinct(new TemplateIdentityEqualityComparer())
                                                    .Select(t => (ITemplateInfo)new BlobStorageTemplateInfo(t))
                                                    .ToList();

            Dictionary<string, PackToTemplateEntry> packToTemplateMap = packSourceCheckResults.PackCheckData
                            .Where(r => r.AnyTemplates)
                            .ToDictionary(
                                r => r.PackInfo.Id,
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

            return new TemplateDiscoveryMetadata("1.0.0.0", templateCache, packToTemplateMap, additionalData);
        }

        private static void WriteNonTemplatePackList(string reportPath, IReadOnlyList<PackCheckResult> packCheckResults)
        {
            List<string> packsWithoutTemplates = packCheckResults.Where(r => !r.AnyTemplates)
                                                                .Select(r => r.PackInfo.Id)
                                                                .ToList();
            string serializedContent = JsonConvert.SerializeObject(packsWithoutTemplates, Formatting.Indented);

            string outputFileName = Path.Combine(reportPath, NonTemplatePacksFileName);
            File.WriteAllText(outputFileName, serializedContent);
            Console.WriteLine($"Non template pack list was created: {outputFileName}");
        }

        private class TemplateIdentityEqualityComparer : IEqualityComparer<ITemplateInfo>
        {
            public bool Equals(ITemplateInfo? x, ITemplateInfo? y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                if (x == null || y == null)
                {
                    return false;
                }
                return string.Equals(x.Identity, y.Identity, System.StringComparison.Ordinal);
            }

            public int GetHashCode(ITemplateInfo obj)
            {
                return obj.Identity.GetHashCode();
            }
        }
    }
}
