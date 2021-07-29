// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;
using Microsoft.TemplateSearch.TemplateDiscovery.NuGet;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    internal partial class LegacyMetadataWriter
    {
        internal static string WriteLegacySearchMetadata(PackSourceCheckResult packSourceCheckResults, string outputFileName)
        {
            var searchMetadata = CreateLegacySearchMetadata(packSourceCheckResults);
            File.WriteAllText(outputFileName, searchMetadata.ToJObject().ToString(Newtonsoft.Json.Formatting.None));
            Console.WriteLine($"Legacy search cache file created: {outputFileName}");
            return outputFileName;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private static TemplateDiscoveryMetadata CreateLegacySearchMetadata(PackSourceCheckResult packSourceCheckResults)
        {
            List<ITemplateInfo> templateCache = packSourceCheckResults.PackCheckData.Where(r => r.AnyTemplates)
                                                    .SelectMany(r => r.FoundTemplates)
                                                    .Distinct(new TemplateIdentityEqualityComparer())
                                                    .Select(t => (ITemplateInfo)new LegacyBlobTemplateInfo(t))
                                                    .ToList();

            Dictionary<string, PackToTemplateEntry> packToTemplateMap = packSourceCheckResults.PackCheckData
                            .Where(r => r.AnyTemplates)
                            .ToDictionary(
                                r => r.PackInfo.Name,
                                r =>
                                {
                                    PackToTemplateEntry packToTemplateEntry = new PackToTemplateEntry(
                                            r.PackInfo.Version ?? "",
                                            r.FoundTemplates.Select(t => new TemplateIdentificationEntry(t.Identity, t.GroupIdentity)).ToList());

                                    if (r.PackInfo is NuGetPackInfo npi)
                                    {
                                        packToTemplateEntry.TotalDownloads = npi.TotalDownloads;
                                    }
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
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
