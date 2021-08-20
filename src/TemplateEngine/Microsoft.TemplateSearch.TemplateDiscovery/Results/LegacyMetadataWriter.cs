// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    [Obsolete]
    internal partial class LegacyMetadataWriter
    {
        internal static string WriteLegacySearchMetadata(PackSourceCheckResult packSourceCheckResults, string outputFileName)
        {
            var searchMetadata = CreateLegacySearchMetadata(packSourceCheckResults);
            File.WriteAllText(outputFileName, searchMetadata.ToJObject().ToString(Newtonsoft.Json.Formatting.None));
            Console.WriteLine($"Legacy search cache file created: {outputFileName}");
            return outputFileName;
        }

        private static TemplateDiscoveryMetadata CreateLegacySearchMetadata(PackSourceCheckResult packSourceCheckResults)
        {
            List<ITemplateInfo> templateCache = packSourceCheckResults.SearchCache.TemplatePackages.SelectMany(p => p.Templates)
                                                    .Distinct(new TemplateIdentityEqualityComparer())
                                                    .Select(t => (ITemplateInfo)new LegacyBlobTemplateInfo(t))
                                                    .ToList();

            Dictionary<string, PackToTemplateEntry> packToTemplateMap = packSourceCheckResults.SearchCache.TemplatePackages
                            .ToDictionary(
                                r => r.Name,
                                r =>
                                {
                                    PackToTemplateEntry packToTemplateEntry = new PackToTemplateEntry(
                                            r.Version ?? "",
                                            r.Templates.Select(t => new TemplateIdentificationEntry(t.Identity, t.GroupIdentity)).ToList());
                                    packToTemplateEntry.TotalDownloads = r.TotalDownloads;
                                    packToTemplateEntry.Owners = r.Owners;
                                    packToTemplateEntry.Verified = r.Verified;
                                    return packToTemplateEntry;
                                });

            Dictionary<string, object> additionalData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (IAdditionalDataProducer dataProducer in packSourceCheckResults.AdditionalDataProducers)
            {
                if (dataProducer.Data != null)
                {
                    additionalData[dataProducer.DataUniqueName] = dataProducer.Data;
                }
            }

            return new TemplateDiscoveryMetadata("1.0.0.3", templateCache, packToTemplateMap, additionalData);
        }
    }
}
