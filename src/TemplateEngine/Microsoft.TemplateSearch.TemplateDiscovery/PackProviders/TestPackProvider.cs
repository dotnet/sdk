// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    internal class TestPackProvider : IPackProvider
    {
        private readonly DirectoryInfo _folder;

        internal TestPackProvider(DirectoryInfo folder)
        {
            _folder = folder;
        }

        public string Name => "TestProvider";

        public Task DeleteDownloadedPacksAsync()
        {
            //do nothing - do not remove test packs
            return Task.FromResult(0);
        }

        public Task<IDownloadedPackInfo?> DownloadPackageAsync(ITemplatePackageInfo packinfo, CancellationToken token)
        {
            return Task.FromResult((IDownloadedPackInfo?)packinfo);
        }

        public async IAsyncEnumerable<ITemplatePackageInfo> GetCandidatePacksAsync([EnumeratorCancellation]CancellationToken token)
        {
            foreach (FileInfo package in _folder.EnumerateFiles("*.nupkg", SearchOption.AllDirectories))
            {
                yield return new TestPackInfo(package.FullName);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public Task<int> GetPackageCountAsync(CancellationToken token)
        {
            return Task.FromResult(_folder.EnumerateFiles("*.nupkg", SearchOption.AllDirectories).Count());
        }

        private class TestPackInfo : ITemplatePackageInfo, IDownloadedPackInfo
        {
            internal TestPackInfo(string path)
            {
                Path = path;
                Name = System.IO.Path.GetFileNameWithoutExtension(path);
            }

            public string Name { get; }

            public string Version => "1.0";

            public long TotalDownloads => 0;

            public string Path { get; }

            public IReadOnlyList<string> Owners => new[] { "TestAuthor" };

            public bool Verified => false;
        }
    }
}
