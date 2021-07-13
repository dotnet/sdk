// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal class ScraperConfig
    {
        internal string? LocalPackagePath { get; set; }

        internal string? BasePath { get; set; }

        internal int PageSize { get; set; }

        internal bool SaveCandidatePacks { get; set; }

        internal bool RunOnlyOnePage { get; set; }

        internal bool IncludePreviewPacks { get; set; }

        internal string? PreviousRunBasePath { get; set; }

        internal bool DontFilterOnTemplateJson { get; set; }

        internal List<string> Providers { get; } = new List<string>();
    }
}
