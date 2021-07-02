// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    internal static class PackCheckResultReportWriter
    {
        internal const string CacheContentDirectory = "SearchCache";
        // All the metadata needed for searching from dotnet new.
        internal const string SearchMetadataFilename = "NuGetTemplateSearchInfo.json";
        internal const string SearchMetadataFilenameVer2 = "NuGetTemplateSearchInfoVer2.json";

        // Metadata for the scraper to skip packs known to not contain templates.
        internal const string NonTemplatePacksFileName = "nonTemplatePacks.json";

        internal static void WriteResults(string outputBasePath, PackSourceCheckResult packSourceCheckResults)
        {
            string reportPath = Path.Combine(outputBasePath, CacheContentDirectory);

            if (!Directory.Exists(reportPath))
            {
                Directory.CreateDirectory(reportPath);
                Console.WriteLine($"Created directory:{reportPath}");
            }
            WriteNonTemplatePackList(reportPath, packSourceCheckResults.PackCheckData);
            LegacyMetadataWriter.WriteLegacySearchMetadata(packSourceCheckResults, Path.Combine(reportPath, SearchMetadataFilename));
            WriteSearchMetadata(packSourceCheckResults, Path.Combine(reportPath, SearchMetadataFilenameVer2));

        }

        private static string WriteSearchMetadata(PackSourceCheckResult packSourceCheckResults, string outputFileName)
        {
            TemplateSearchCache searchMetadata = CreateSearchMetadata(packSourceCheckResults);
            File.WriteAllText(outputFileName, searchMetadata.ToJObject().ToString());
            Console.WriteLine($"Search cache file created: {outputFileName}");
            return outputFileName;
        }

        private static TemplateSearchCache CreateSearchMetadata(PackSourceCheckResult packSourceCheckResults)
        {
            List<TemplatePackageSearchData> packages = new List<TemplatePackageSearchData>();
            foreach (PackCheckResult package in packSourceCheckResults.PackCheckData)
            {
                List<TemplateSearchData> templates = new List<TemplateSearchData>();
                foreach (ITemplateInfo template in package.FoundTemplates)
                {
                    Dictionary<string, object> data = new Dictionary<string, object>();
                    foreach (var producer in packSourceCheckResults.AdditionalDataProducers)
                    {
                        var producerData = producer.GetDataForTemplate(package.PackInfo, template.Identity);
                        if (producerData != null)
                        {
                            data[producer.DataUniqueName] = producerData;
                        }
                    }
                    templates.Add(new TemplateSearchData(template, data));
                }
                Dictionary<string, object> packData = new Dictionary<string, object>();
                foreach (var producer in packSourceCheckResults.AdditionalDataProducers)
                {
                    var producerData = producer.GetDataForPack(package.PackInfo);
                    if (producerData != null)
                    {
                        packData[producer.DataUniqueName] = producerData;
                    }
                }
                packages.Add(new TemplatePackageSearchData(new PackInfo(package.PackInfo.Id, package.PackInfo.Version, package.PackInfo.TotalDownloads), templates, packData));
            }
            return new TemplateSearchCache(packages);
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
    }
}
