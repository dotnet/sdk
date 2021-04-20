// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackInfo : IDownloadedPackInfo
    {
        public string VersionedPackageIdentity { get; set; }

        public string Id { get; set; }

        public string Version { get; set; }

        public string Path { get; set; }

        public long TotalDownloads { get; set; }
    }
}
