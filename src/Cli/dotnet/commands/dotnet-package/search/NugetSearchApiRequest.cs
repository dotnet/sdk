// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.Diagnostics;
using LocalizableStrings = Microsoft.DotNet.Tools.Package.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli.commands.package.search
{
    internal class NugetSearchApiRequest
    {
        private readonly string _sourceSeparator = new string('=', 20);
        private readonly string _packageSeparator = new string('-', 20);
        private string _searchTerm;
        private int? _skip;
        private int? _take;
        private bool _prerelease;
        private List<string> _sources;
        private bool _exactMatch;

        public NugetSearchApiRequest(string searchTerm, int? skip, int? take, bool prerelease, bool exactMatch, List<string> sources)
        {
            _searchTerm = searchTerm;
            _skip = skip;
            _take = take;
            _prerelease = prerelease;
            _sources = sources;
            _exactMatch = exactMatch;
        }
        public async Task ExecuteCommandAsync()
        {
            var taskList = new List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>();
            IList<PackageSource> listEndpoints = GetEndpointsAsync();
            WarnForHTTPSources(listEndpoints);
            foreach (PackageSource source in listEndpoints)
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(source);
                var cancellation = CancellationToken.None;
                PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>(cancellation);
                if (resource is null)
                {
                    taskList.Add((null, source));
                    continue;
                }
                taskList.Add((Task.Run(() => resource.SearchAsync(
                    _searchTerm,
                    new SearchFilter(includePrerelease: _prerelease),
                    skip: _skip ?? 0,
                    take: _take ?? 20,
                    NullLogger.Instance,
                    CancellationToken.None
                    )), source));
            }

            foreach (var taskItem in taskList)
            {
                var (task, source) = taskItem;

                if (task is null)
                {
                    Console.WriteLine(_sourceSeparator);
                    Console.WriteLine($"Source: {source.Name}");
                    Console.WriteLine(_packageSeparator);
                    Console.WriteLine("Failed to obtain a search resource.");
                    Console.WriteLine(_packageSeparator);
                    Console.WriteLine();
                    continue;
                }

                var results = await task;

                Console.WriteLine(_sourceSeparator);
                Console.WriteLine($"Source: {source.Name}"); // System.Console is used so that output is not suppressed by Verbosity.
                PrintResult(results);
                Console.WriteLine(_packageSeparator);
            }
        }

        private void PrintResult(IEnumerable<IPackageSearchMetadata> results)
        {
            if (_exactMatch && results?.Any() == true &&
                results.First().Identity.Id.Equals(_searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                // we are doing exact match and if the result from the API are sorted, the first result should be the package we are searching
                IPackageSearchMetadata result = results.First();
                Console.WriteLine($"{result.Identity.Id} | {result.Identity.Version} | Downloads: {(result.DownloadCount.HasValue ? result.DownloadCount.ToString() : "N/A")}");
                return;
            }
            else
            {
                foreach (IPackageSearchMetadata result in results)
                {
                    Console.WriteLine($"{result.Identity.Id} | {result.Identity.Version} | Downloads: {(result.DownloadCount.HasValue ? result.DownloadCount.ToString() : "N/A")}");
                }
            }
        }

        private IList<PackageSource> GetEndpointsAsync()
        {
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(),
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            List<PackageSource> configurationSources = sourceProvider.LoadPackageSources()
                .Where(p => p.IsEnabled)
                .ToList();

            IList<PackageSource> packageSources;
            if (_sources.Count > 0)
            {
                packageSources = _sources
                    .Select(s => ResolveSource(configurationSources, s))
                    .ToList();
            }
            else
            {
                packageSources = configurationSources;
            }
            return packageSources;
        }

        private static PackageSource ResolveSource(IEnumerable<PackageSource> availableSources, string source)
        {
            var resolvedSource = availableSources.FirstOrDefault(
                    f => f.Source.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                        f.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

            if (resolvedSource == null)
            {
                ValidateSource(source);
                return new PackageSource(source);
            }
            else
            {
                return resolvedSource;
            }
        }

        public static void ValidateSource(string source)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out Uri result))
            {
                throw new Exception("Invalid source " + source);
            }
        }

        private void WarnForHTTPSources(IList<PackageSource> packageSources)
        {
            List<PackageSource> httpPackageSources = null;
            foreach (PackageSource packageSource in packageSources)
            {
                if (packageSource.IsHttp && !packageSource.IsHttps)
                {
                    if (httpPackageSources == null)
                    {
                        httpPackageSources = new();
                    }
                    httpPackageSources.Add(packageSource);
                }
            }

            if (httpPackageSources != null && httpPackageSources.Count != 0)
            {
                if (httpPackageSources.Count == 1)
                {
                    Console.WriteLine(
                        string.Format(CultureInfo.CurrentCulture,
                        LocalizableStrings.Warning_HttpServerUsage,
                        "search",
                        httpPackageSources[0]));
                }
                else
                {
                    Console.WriteLine(
                        string.Format(CultureInfo.CurrentCulture,
                        LocalizableStrings.Warning_HttpServerUsage,
                        "search",
                        Environment.NewLine + string.Join(Environment.NewLine, httpPackageSources.Select(e => e.Name))));
                }
            }
        }
    }
}
