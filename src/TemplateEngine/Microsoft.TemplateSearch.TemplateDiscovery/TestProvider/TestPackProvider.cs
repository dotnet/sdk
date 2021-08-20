// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Test
{
    internal class TestPackProvider : IPackProvider
    {
        private readonly DirectoryInfo _folder;

        internal TestPackProvider(DirectoryInfo folder)
        {
            _folder = folder;
        }

        public string Name => "TestFolderProvider";

        public Task DeleteDownloadedPacksAsync()
        {
            //do nothing - do not remove test packs
            return Task.FromResult(0);
        }

        public Task<IDownloadedPackInfo> DownloadPackageAsync(ITemplatePackageInfo packinfo, CancellationToken token)
        {
            return Task.FromResult((IDownloadedPackInfo)packinfo);
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

        public Task<(ITemplatePackageInfo PackageInfo, bool Removed)> GetPackageInfoAsync(string packageIdentifier, CancellationToken cancellationToken)
        {
            foreach (FileInfo package in _folder.EnumerateFiles("*.nupkg", SearchOption.AllDirectories))
            {
                if (package.Name.Contains(packageIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(((ITemplatePackageInfo)new TestPackInfo(package.FullName), false));
                }
            }
            return Task.FromResult(((ITemplatePackageInfo)new TestPackInfo(packageIdentifier), true));
        }

        private class TestPackInfo : ITemplatePackageInfo, IDownloadedPackInfo
        {
            internal TestPackInfo(string path)
            {
                Path = path;

                string filename = System.IO.Path.GetFileNameWithoutExtension(path);

                //for testing purposes NuGet versioned packages should be named as <name>##<version>.nupkg
                if (filename.Contains("##"))
                {
                    string[] split = filename.Split("##");
                    Name = split[0];
                    Version = split[1];
                }
                else
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(path);
                    Version = "1.0";
                }
            }

            public string Name { get; }

            public string Version { get; }

            public long TotalDownloads => 0;

            public string Path { get; }

            public IReadOnlyList<string> Owners => new[] { "TestAuthor" };

            public bool Verified => false;
        }
    }
}
