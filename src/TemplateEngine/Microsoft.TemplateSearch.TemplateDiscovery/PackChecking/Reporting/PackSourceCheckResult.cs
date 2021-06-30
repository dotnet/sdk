// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting
{
    internal class PackSourceCheckResult
    {
        internal PackSourceCheckResult(IReadOnlyList<PackCheckResult> packCheckData, IReadOnlyList<IAdditionalDataProducer> additionalDataHandlers)
        {
            PackCheckData = packCheckData;
            AdditionalDataProducers = additionalDataHandlers;
        }

        internal IReadOnlyList<PackCheckResult> PackCheckData { get; }

        internal IReadOnlyList<IAdditionalDataProducer> AdditionalDataProducers { get; }
    }
}
