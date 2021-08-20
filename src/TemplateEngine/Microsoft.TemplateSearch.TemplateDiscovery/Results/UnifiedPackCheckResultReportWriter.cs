// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
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

        internal static (string MetadataPath, string LegacyMetadataPath) WriteResults(DirectoryInfo outputBasePath, PackSourceCheckResult packSourceCheckResults)
        {
            string reportPath = Path.Combine(outputBasePath.FullName, CacheContentDirectory);

            if (!Directory.Exists(reportPath))
            {
                Directory.CreateDirectory(reportPath);
                Console.WriteLine($"Created directory:{reportPath}");
            }

            string legacyMetadataFilePath = Path.Combine(reportPath, SearchMetadataFilename);
            string metadataFilePath = Path.Combine(reportPath, SearchMetadataFilenameVer2);

            WriteNonTemplatePackList(reportPath, packSourceCheckResults.FilteredPackages);
            #pragma warning disable CS0612 // Type or member is obsolete
            LegacyMetadataWriter.WriteLegacySearchMetadata(packSourceCheckResults, legacyMetadataFilePath);
#pragma warning restore CS0612 // Type or member is obsolete
            WriteSearchMetadata(packSourceCheckResults, metadataFilePath);
            return (metadataFilePath, legacyMetadataFilePath);

        }

        private static void WriteSearchMetadata(PackSourceCheckResult packSourceCheckResults, string outputFileName)
        {
            TemplateSearchCache searchMetadata = packSourceCheckResults.SearchCache;
            File.WriteAllText(outputFileName, searchMetadata.ToJObject().ToString(Formatting.None));
            Console.WriteLine($"Search cache file created: {outputFileName}");
        }

        private static void WriteNonTemplatePackList(string reportPath, IReadOnlyList<FilteredPackageInfo> packCheckResults)
        {
            string serializedContent = JsonConvert.SerializeObject(packCheckResults, Formatting.None);
            string outputFileName = Path.Combine(reportPath, NonTemplatePacksFileName);
            File.WriteAllText(outputFileName, serializedContent);
            Console.WriteLine($"Non template pack list was created: {outputFileName}");
        }
    }
}
