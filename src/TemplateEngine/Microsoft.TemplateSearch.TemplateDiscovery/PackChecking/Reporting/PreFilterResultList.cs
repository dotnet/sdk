// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting
{
    internal class PreFilterResultList
    {
        internal PreFilterResultList()
        {
            Results = new List<PreFilterResult>();
        }

        internal PreFilterResultList(List<PreFilterResult> results)
        {
            Results = results;
        }

        internal IReadOnlyList<PreFilterResult> Results { get; }

        // return true if any of the filter results have IsFiltered == true
        internal bool ShouldBeFiltered
        {
            get
            {
                return Results.Any(r => r.IsFiltered);
            }
        }

        internal string Reason
        {
            get
            {
                return string.Join("; ", Results.Where(r => r.IsFiltered && !string.IsNullOrWhiteSpace(r.Reason)).Select(r => r.Reason));
            }
        }
    }
}
