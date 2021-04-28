// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting
{
    public class PreFilterResult
    {
        public string FilterId { get; set; }

        public bool IsFiltered { get; set; }

        public string Reason { get; set; }
    }
}
