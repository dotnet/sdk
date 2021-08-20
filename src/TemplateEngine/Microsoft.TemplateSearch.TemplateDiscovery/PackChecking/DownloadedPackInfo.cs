// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    public class DownloadedPackInfo : IDownloadedPackInfo
    {
        private ITemplatePackageInfo _info;

        internal DownloadedPackInfo(ITemplatePackageInfo info, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or whitespace.", nameof(filePath));
            }

            _info = info ?? throw new ArgumentNullException(nameof(info));
            Path = filePath;
        }

        public string Name => _info.Name;

        public string? Version => _info.Version;

        public string Path { get; private set; }

        public long TotalDownloads => _info.TotalDownloads;

        public IReadOnlyList<string> Owners => _info.Owners;

        public bool Verified => _info.Verified;
    }
}
