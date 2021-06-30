// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting
{
    internal class PreFilterResult
    {
        internal PreFilterResult(string filterId, bool isFiltered, string? reason = null)
        {
            if (string.IsNullOrWhiteSpace(filterId))
            {
                throw new System.ArgumentException($"'{nameof(filterId)}' cannot be null or whitespace.", nameof(filterId));
            }
            FilterId = filterId;
            IsFiltered = isFiltered;
            Reason = reason;
        }

        internal string FilterId { get; private set; }

        internal bool IsFiltered { get; private set; }

        internal string? Reason { get; private set; }
    }
}
