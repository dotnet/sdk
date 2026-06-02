// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal class PackSourceCheckResult
    {
        internal PackSourceCheckResult(TemplateSearchCache generatedSearchCache, IReadOnlyList<FilteredPackageInfo> filteredPackages, IReadOnlyList<IAdditionalDataProducer> additionalDataHandlers)
        {
            SearchCache = generatedSearchCache;
            FilteredPackages = filteredPackages;
            AdditionalDataProducers = additionalDataHandlers;
        }

        internal TemplateSearchCache SearchCache { get; }

        internal IReadOnlyList<FilteredPackageInfo> FilteredPackages { get; private set; }

        internal IReadOnlyList<IAdditionalDataProducer> AdditionalDataProducers { get; }
    }
}
