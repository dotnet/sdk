// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateSearch.Common
{
    public class PackToTemplateEntry
    {
        public PackToTemplateEntry(string version, List<TemplateIdentificationEntry> templateinfo)
        {
            Version = version;
            TemplateIdentificationEntry = templateinfo;
        }

        public string Version { get; }

        public long TotalDownloads { get; set; }

        public IReadOnlyList<TemplateIdentificationEntry> TemplateIdentificationEntry { get; }
    }
}
