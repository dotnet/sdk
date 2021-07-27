// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Microsoft.TemplateSearch.TemplateDiscovery.Results;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Filters
{
    internal sealed class PreviouslyRejectedPackFilter
    {
        private const string FilterId = "Previously Seen";

        internal static Func<IDownloadedPackInfo, PreFilterResult> SetupPackFilter(HashSet<string> nonTemplatePacks)
        {
            Func<IDownloadedPackInfo, PreFilterResult> previouslyRejectedPackFilter = (packInfo) =>
            {
                if (nonTemplatePacks.Contains(packInfo.Name))
                {
                    return new PreFilterResult(FilterId, isFiltered: true, "Package was previously examined, and does not contain templates.");
                }

                return new PreFilterResult(FilterId, isFiltered: false);
            };

            return previouslyRejectedPackFilter;
        }

        internal static bool TryGetPreviouslySkippedPacks(string previousRunBasePath, out HashSet<string>? nonTemplatePacks)
        {
            if (string.IsNullOrEmpty(previousRunBasePath))
            {
                nonTemplatePacks = new HashSet<string>();
                return true;
            }
            else if (Directory.Exists(previousRunBasePath))
            {
                string nonTemplatePackDataFile = Path.Combine(previousRunBasePath, PackCheckResultReportWriter.CacheContentDirectory, PackCheckResultReportWriter.NonTemplatePacksFileName);
                string fileContents = File.ReadAllText(nonTemplatePackDataFile);
                nonTemplatePacks = JsonConvert.DeserializeObject<HashSet<string>>(fileContents);
                return true;
            }

            nonTemplatePacks = null;
            return false;
        }
    }
}
