// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    internal class TestPackProvider : IPackProvider
    {
        private readonly string _folder;

        internal TestPackProvider(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException($"'{nameof(folder)}' cannot be null or whitespace.", nameof(folder));
            }
            _folder = folder;
        }

        public string Name => "TestProvider";

        public void DeleteDownloadedPacks()
        {
            //do nothing - do not remove test packs
        }

        public Task<IDownloadedPackInfo> DownloadPackageAsync(IPackInfo packinfo)
        {
            return Task.FromResult((IDownloadedPackInfo)packinfo);
        }

        public async IAsyncEnumerable<IPackInfo> GetCandidatePacksAsync()
        {
            var directoryInfo = new DirectoryInfo(_folder);

            foreach (FileInfo package in directoryInfo.EnumerateFiles("*.nupkg", SearchOption.AllDirectories))
            {
                yield return new TestPackInfo(package.FullName);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public Task<int> GetPackageCountAsync()
        {
            return Task.FromResult(new DirectoryInfo(_folder).EnumerateFiles("*.nupkg", SearchOption.AllDirectories).Count());
        }

        private class TestPackInfo : IPackInfo, IDownloadedPackInfo
        {
            internal TestPackInfo(string path)
            {
                Path = path;
                Id = System.IO.Path.GetFileNameWithoutExtension(path);
            }

            public string Id { get; }

            public string Version => "1.0";

            public long TotalDownloads => 0;

            public string VersionedPackageIdentity => $"{Id}::{Version}";

            public string Path { get; }
        }
    }
}
