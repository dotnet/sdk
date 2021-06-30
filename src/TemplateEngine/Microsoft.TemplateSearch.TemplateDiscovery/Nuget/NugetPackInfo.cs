// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackInfo : IDownloadedPackInfo
    {
        private IPackInfo _info;

        internal NugetPackInfo(IPackInfo info, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new System.ArgumentException($"'{nameof(filePath)}' cannot be null or whitespace.", nameof(filePath));
            }

            _info = info ?? throw new System.ArgumentNullException(nameof(info));
            Path = filePath;
        }

        public string VersionedPackageIdentity => $"{Id}::{Version}";

        public string Id => _info.Id;

        public string Version => _info.Version;

        public string Path { get; private set; }

        public long TotalDownloads => _info.TotalDownloads;
    }
}
