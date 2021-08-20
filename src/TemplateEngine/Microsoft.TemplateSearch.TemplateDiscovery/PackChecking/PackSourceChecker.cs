// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal class PackSourceChecker
    {
        private const string HostIdentifierBase = "dotnetcli-discovery-";
        private readonly IEnumerable<IPackProvider> _packProviders;
        private readonly PackPreFilterer _packPreFilterer;
        private readonly IReadOnlyList<IAdditionalDataProducer> _additionalDataProducers;
        private readonly bool _saveCandidatePacks;
        private readonly IReadOnlyDictionary<string, TemplatePackageSearchData> _existingCache;
        private readonly IReadOnlyDictionary<string, FilteredPackageInfo> _knownFilteredPackages;
        private readonly bool _diffEnabled;

        internal PackSourceChecker(
            IEnumerable<IPackProvider> packProviders,
            PackPreFilterer packPreFilterer,
            IReadOnlyList<IAdditionalDataProducer> additionalDataProducers,
            bool saveCandidatePacks,
            TemplateSearchCache? existingData = null,
            IEnumerable<FilteredPackageInfo>? knownFilteredPackages = null)
        {
            _packProviders = packProviders;
            _packPreFilterer = packPreFilterer;
            _additionalDataProducers = additionalDataProducers;
            _saveCandidatePacks = saveCandidatePacks;

            _existingCache = existingData?.TemplatePackages.ToDictionary(p => p.Name) ?? new Dictionary<string, TemplatePackageSearchData>();
            _knownFilteredPackages = knownFilteredPackages?.ToDictionary(item => item.Name) ?? new Dictionary<string, FilteredPackageInfo>();
            _diffEnabled = _existingCache.Any();
        }

        internal async Task<PackSourceCheckResult> CheckPackagesAsync(CancellationToken token)
        {
            Dictionary<string, TemplatePackageSearchData> newCache = new Dictionary<string, TemplatePackageSearchData>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, FilteredPackageInfo> filteredPackages = new Dictionary<string, FilteredPackageInfo>(StringComparer.OrdinalIgnoreCase);

            ScanningStats scanningStats = new ScanningStats();

            try
            {
                await ProcessTemplatePackagesUsingSearchAsync(newCache, filteredPackages, scanningStats, token).ConfigureAwait(false);
                if (_diffEnabled)
                {
                    await CheckRemovedPackagesAsync(newCache, filteredPackages, scanningStats, token).ConfigureAwait(false);
                }
                DisplayScanningStats(scanningStats);
                return new PackSourceCheckResult(new TemplateSearchCache(newCache.Values.ToList()), filteredPackages.Values.ToList(), _additionalDataProducers);
            }
            finally
            {
                if (!_saveCandidatePacks)
                {
                    Console.WriteLine("Removing downloaded packs");
                    foreach (IPackProvider provider in _packProviders)
                    {
                        await provider.DeleteDownloadedPacksAsync().ConfigureAwait(false);
                    }
                    Console.WriteLine("Downloaded packs were removed");
                }
            }
        }

        private async Task ProcessTemplatePackagesUsingSearchAsync(
            Dictionary<string, TemplatePackageSearchData> newCache,
            Dictionary<string, FilteredPackageInfo> filteredPackages,
            ScanningStats scanningStats,
            CancellationToken cancellationToken)
        {
            foreach (IPackProvider packProvider in _packProviders)
            {
                int count = -1;
                Console.WriteLine($"Processing pack provider {packProvider.Name}:");
                Console.WriteLine($"{await packProvider.GetPackageCountAsync(cancellationToken).ConfigureAwait(false)} packs discovered, starting processing");

                await foreach (ITemplatePackageInfo sourceInfo in packProvider.GetCandidatePacksAsync(cancellationToken).ConfigureAwait(false))
                {
                    count = ProcessCount(count);
                    if (newCache.ContainsKey(sourceInfo.Name) || filteredPackages.ContainsKey(sourceInfo.Name))
                    {
                        Verbose.WriteLine($"Package {sourceInfo.Name}::{sourceInfo.Version} is already processed.");
                        continue;
                    }
                    string? oldTemplateVersion = null;
                    string? oldNonTemplateVersion = null;
                    if (_diffEnabled)
                    {
                        if (CheckIfVersionIsKnownTemplatePackage(sourceInfo, newCache, scanningStats, out oldTemplateVersion))
                        {
                            continue;
                        }
                        if (CheckIfVersionIsKnownNonTemplatePackage(sourceInfo, filteredPackages, scanningStats, out oldNonTemplateVersion))
                        {
                            continue;
                        }
                    }

                    IDownloadedPackInfo? packInfo = await packProvider.DownloadPackageAsync(sourceInfo, cancellationToken).ConfigureAwait(false);
                    if (CheckIfPackageIsFiltered(packInfo, filteredPackages, scanningStats, oldTemplateVersion, oldNonTemplateVersion))
                    {
                        continue;
                    }
                    ProcessTemplatePackage(packInfo, newCache, filteredPackages, scanningStats, oldTemplateVersion, oldNonTemplateVersion);
                }
                ProcessCount(count);
                Console.WriteLine($"All packs from pack provider {packProvider.Name} are processed");
            }
            Console.WriteLine("All packs are processed");
        }

        private async Task CheckRemovedPackagesAsync(
            Dictionary<string, TemplatePackageSearchData> newCache,
            Dictionary<string, FilteredPackageInfo> filteredPackages,
            ScanningStats scanningStats,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<TemplatePackageSearchData> removedTemplatePacks;
            IReadOnlyList<FilteredPackageInfo> removedNonTemplatePacks;

            (removedTemplatePacks, removedNonTemplatePacks) = EvaluateRemovedPackages(newCache, filteredPackages);
            if (!removedTemplatePacks.Any() && removedNonTemplatePacks.Any())
            {
                return;
            }

            var provider = _packProviders.FirstOrDefault();
            if (provider == null)
            {
                Console.WriteLine($"Providers do not support checking information about package, removed packages won't be verified and added to cache.");
                return;
            }

            Console.WriteLine("Checking template packages via API: ");
            foreach (var package in removedTemplatePacks)
            {
                var packageInfo = await provider.GetPackageInfoAsync(package.Name, cancellationToken).ConfigureAwait(false);
                if (packageInfo == default)
                {
                    Console.WriteLine($"Package {package.Name} cannot be verified.");
                    throw new Exception($"Package {package.Name} is missing and cannot be verified.");
                }
                if (packageInfo.Removed)
                {
                    Console.WriteLine($"Package {package.Name} was unlisted.");
                    scanningStats.RemovedTemplatePacks.Add(package);
                }
                else
                {
                    Console.WriteLine($"Package {package.Name} was verified, adding to cache.");
                    if (CheckIfVersionIsKnownTemplatePackage(package, newCache, scanningStats, out string? oldVersion))
                    {
                        continue;
                    }

                    IDownloadedPackInfo? packInfo = await provider.DownloadPackageAsync(package, cancellationToken).ConfigureAwait(false);
                    if (CheckIfPackageIsFiltered(packInfo, filteredPackages, scanningStats, oldVersion, null))
                    {
                        continue;
                    }
                    ProcessTemplatePackage(packInfo, newCache, filteredPackages, scanningStats, oldVersion, null);
                }
            }

            Verbose.WriteLine("Checking non template packages via API: ");
            foreach (var package in removedNonTemplatePacks)
            {
                var packageInfo = await provider.GetPackageInfoAsync(package.Name, cancellationToken).ConfigureAwait(false);
                if (packageInfo == default)
                {
                    Verbose.WriteLine($"Package {package.Name} cannot be verified.");
                    //it's ok to continue if the package was known non-package - ignore error
                    scanningStats.UnavailableNonTemplatePacksCount++;
                    continue;
                }
                if (packageInfo.Removed)
                {
                    Verbose.WriteLine($"Package {package.Name} was unlisted.");
                    scanningStats.RemovedNonTemplatePacksCount++;
                }
                else
                {
                    Verbose.WriteLine($"Package {package.Name} was verified.");
                    if (CheckIfVersionIsKnownNonTemplatePackage(package, filteredPackages, scanningStats, out string? oldNonTemplateVersion))
                    {
                        continue;
                    }

                    IDownloadedPackInfo? packInfo = await provider.DownloadPackageAsync(package, cancellationToken).ConfigureAwait(false);
                    if (packInfo == null)
                    {
                        Console.WriteLine($"[Error] Package {package.Name}::{package.Version} is not processed.");
                        continue;
                    }
                    if (CheckIfPackageIsFiltered(packInfo, filteredPackages, scanningStats, null, oldNonTemplateVersion))
                    {
                        continue;
                    }
                    ProcessTemplatePackage(packInfo, newCache, filteredPackages, scanningStats, null, oldNonTemplateVersion);
                }
            }

        }

        private bool CheckIfVersionIsKnownTemplatePackage(
            ITemplatePackageInfo sourceInfo,
            Dictionary<string, TemplatePackageSearchData> newCache,
            ScanningStats scanningStats,
            out string? currentVersion)
        {
            currentVersion = null;
            if (_existingCache.TryGetValue(sourceInfo.Name, out TemplatePackageSearchData? existingInfo))
            {
                if (sourceInfo.Version == existingInfo.Version)
                {
                    Verbose.WriteLine($"Package {sourceInfo.Name}::{sourceInfo.Version} has not changed since last scan, updating metadata only.");
                    newCache[sourceInfo.Name] = new TemplatePackageSearchData(sourceInfo, existingInfo.Templates, sourceInfo.ProduceAdditionalData(_additionalDataProducers));

                    scanningStats.SameTemplatePacksCount++;
                    return true;
                }
                else
                {
                    currentVersion = existingInfo.Version;
                }
            }
            return false;
        }

        private bool CheckIfVersionIsKnownNonTemplatePackage(
            ITemplatePackageInfo sourceInfo,
            Dictionary<string, FilteredPackageInfo> filteredPackages,
            ScanningStats scanningStats,
            out string? currentVersion)
        {
            currentVersion = null;
            if (_knownFilteredPackages.TryGetValue(sourceInfo.Name, out FilteredPackageInfo? info))
            {
                if (sourceInfo.Version == info.Version)
                {
                    Verbose.WriteLine($"Package {sourceInfo.Name}::{sourceInfo.Version} has not changed since last scan, skipping as it was filtered last time.");
                    filteredPackages[sourceInfo.Name] = info;
                    scanningStats.SameNonTemplatePacksCount++;
                    return true;
                }
                else
                {
                    currentVersion = info.Version;
                }
            }
            return false;
        }

        private bool CheckIfPackageIsFiltered(
            IDownloadedPackInfo sourceInfo,
            Dictionary<string, FilteredPackageInfo> filteredPackages,
            ScanningStats scanningStats,
            string? oldTemplatePackageVersion,
            string? oldNonTemplatePackageVersion)
        {
            PreFilterResultList preFilterResult = _packPreFilterer.FilterPack(sourceInfo);
            if (preFilterResult.ShouldBeFiltered)
            {
                ProcessNonTemplatePackage(sourceInfo, preFilterResult.Reason, filteredPackages, scanningStats, oldTemplatePackageVersion, oldNonTemplatePackageVersion);
            }
            return preFilterResult.ShouldBeFiltered;
        }

        private void ProcessTemplatePackage(
            IDownloadedPackInfo sourceInfo,
            Dictionary<string, TemplatePackageSearchData> newCache,
            Dictionary<string, FilteredPackageInfo> filteredPackages,
            ScanningStats scanningStats,
            string? oldTemplatePackageVersion,
            string? oldNonTemplatePackageVersion
            )
        {
            IEnumerable<TemplateSearchData> foundTemplates = TryGetTemplatesInPack(sourceInfo, _additionalDataProducers);
            Verbose.WriteLine($"{sourceInfo.Name}::{sourceInfo.Version} is processed");
            if (foundTemplates.Any())
            {
                Verbose.WriteLine("Found templates:");
                foreach (TemplateSearchData template in foundTemplates)
                {
                    string shortNames = string.Join(",", template.ShortNameList);
                    Verbose.WriteLine($"  - {template.Identity} ({shortNames}) by {template.Author}, group: {(string.IsNullOrWhiteSpace(template.GroupIdentity) ? "<not set>" : template.GroupIdentity)}, precedence: {template.Precedence}");
                }
                newCache[sourceInfo.Name] = new TemplatePackageSearchData(sourceInfo, foundTemplates, sourceInfo.ProduceAdditionalData(_additionalDataProducers));
                if (string.IsNullOrWhiteSpace(oldNonTemplatePackageVersion))
                {
                    if (string.IsNullOrWhiteSpace(oldTemplatePackageVersion))
                    {
                        scanningStats.NewTemplatePacks.Add(newCache[sourceInfo.Name]);
                    }
                    else
                    {
                        scanningStats.UpdatedTemplatePacks.Add((newCache[sourceInfo.Name], oldTemplatePackageVersion));
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(oldTemplatePackageVersion))
                    {
                        scanningStats.BecameTemplatePacks.Add((newCache[sourceInfo.Name]));
                    }
                }
            }
            else
            {
                ProcessNonTemplatePackage(
                    sourceInfo,
                    "Failed to scan the package for the templates, the package may contain invalid templates.",
                    filteredPackages,
                    scanningStats,
                    oldTemplatePackageVersion,
                    oldNonTemplatePackageVersion);
            }

        }

        private void ProcessNonTemplatePackage(
            IDownloadedPackInfo sourceInfo,
            string filterReason,
            Dictionary<string, FilteredPackageInfo> filteredPackages,
            ScanningStats scanningStats,
            string? oldTemplatePackageVersion,
            string? oldNonTemplatePackageVersion)
        {
            Verbose.WriteLine($"{sourceInfo.Name}::{sourceInfo.Version} is skipped, {filterReason}");
            filteredPackages[sourceInfo.Name] = new FilteredPackageInfo(sourceInfo, filterReason);
            if (string.IsNullOrWhiteSpace(oldNonTemplatePackageVersion))
            {
                if (string.IsNullOrWhiteSpace(oldTemplatePackageVersion))
                {
                    scanningStats.NewNonTemplatePacksCount++;
                }
                else
                {
                    scanningStats.BecameNonTemplatePacksCount++;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(oldTemplatePackageVersion))
                {
                    scanningStats.UpdatedNonTemplatePacksCount++;
                }
            }
        }

        private IEnumerable<TemplateSearchData> TryGetTemplatesInPack(IDownloadedPackInfo packInfo, IReadOnlyList<IAdditionalDataProducer> additionalDataProducers)
        {
            ITemplateEngineHost host = TemplateEngineHostHelper.CreateHost(HostIdentifierBase + packInfo.Name);
            EngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            Scanner scanner = new Scanner(environmentSettings);
            try
            {
                using var scanResult = scanner.Scan(packInfo.Path, scanForComponents: false);
                if (scanResult.Templates.Any())
                {
                    foreach (IAdditionalDataProducer dataProducer in additionalDataProducers)
                    {
#pragma warning disable CS0612 // Type or member is obsolete
                        dataProducer.CreateDataForTemplatePack(packInfo, scanResult.Templates, environmentSettings);
#pragma warning restore CS0612 // Type or member is obsolete
                    }

                    return scanResult.Templates.Select(t => new TemplateSearchData(t, t.ProduceAdditionalData(additionalDataProducers, environmentSettings)));
                }
                return Array.Empty<TemplateSearchData>();
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read package {0}::{1}, details: {2}. The package will be skipped.", packInfo.Name, packInfo.Version, ex);
                return Array.Empty<TemplateSearchData>();
            }
        }

        private (IReadOnlyList<TemplatePackageSearchData>, IReadOnlyList<FilteredPackageInfo>) EvaluateRemovedPackages(
            Dictionary<string, TemplatePackageSearchData> newCache,
            Dictionary<string, FilteredPackageInfo> filteredPackages)
        {
            List<TemplatePackageSearchData> removedTemplatePacks = new List<TemplatePackageSearchData>();
            List<FilteredPackageInfo> removedNonTemplatePacks = new List<FilteredPackageInfo>();

            foreach (var package in _existingCache.Keys)
            {
                if (!newCache.ContainsKey(package))
                {
                    removedTemplatePacks.Add(_existingCache[package]);
                }
            }

            foreach (var package in _knownFilteredPackages.Keys)
            {
                if (!filteredPackages.ContainsKey(package))
                {
                    removedNonTemplatePacks.Add(_knownFilteredPackages[package]);
                }
            }
            if (removedTemplatePacks.Any())
            {
                Console.WriteLine($"[Error]: the following {removedTemplatePacks.Count} packages were removed");
                foreach (var package in removedTemplatePacks)
                {
                    Console.WriteLine($"   {package.Name}::{package.Version}");
                }
            }
            if (removedNonTemplatePacks.Any())
            {
                Console.WriteLine($"[Error]: the following {removedNonTemplatePacks.Count} non template packages were removed");
                foreach (var package in removedNonTemplatePacks)
                {
                    Console.WriteLine($"   {package.Name}::{package.Version}");
                }
            }
            return (removedTemplatePacks, removedNonTemplatePacks);
        }

        private void DisplayScanningStats(ScanningStats scanningStats)
        {
            Console.WriteLine("Run summary:");
            Console.WriteLine("Template packages:");
            Console.WriteLine($"   new: {scanningStats.NewTemplatePacks.Count}");
            foreach (var package in scanningStats.NewTemplatePacks)
            {
                Console.WriteLine($"      {package.Name}::{package.Version}");
            }

            Console.WriteLine($"   updated: {scanningStats.UpdatedTemplatePacks.Count}");
            foreach (var package in scanningStats.UpdatedTemplatePacks)
            {
                Console.WriteLine($"      {package.Package.Name}, {package.OldVersion} --> {package.Package.Version}");
            }

            Console.WriteLine($"   removed: {scanningStats.RemovedTemplatePacks.Count}");
            foreach (var package in scanningStats.RemovedTemplatePacks)
            {
                Console.WriteLine($"      {package.Name}::{package.Version}");
            }
            if (scanningStats.BecameTemplatePacks.Any())
            {
                Console.WriteLine($"   became template packages: {scanningStats.BecameTemplatePacks.Count}");
                foreach (var package in scanningStats.BecameTemplatePacks)
                {
                    Console.WriteLine($"      {package.Name}::{package.Version}");
                }
            }
            Console.WriteLine($"   not changed: {scanningStats.SameTemplatePacksCount}");
            Console.WriteLine($"Non template packages:");
            Console.WriteLine($"   new: {scanningStats.NewNonTemplatePacksCount}");
            Console.WriteLine($"   updated: {scanningStats.UpdatedNonTemplatePacksCount}");
            Console.WriteLine($"   removed: {scanningStats.RemovedNonTemplatePacksCount}");
            Console.WriteLine($"   not changed: {scanningStats.SameNonTemplatePacksCount}");
            Console.WriteLine($"   unavailable: {scanningStats.UnavailableNonTemplatePacksCount}");
            if (scanningStats.BecameNonTemplatePacksCount > 0)
            {
                Console.WriteLine($"   became non template package: {scanningStats.BecameNonTemplatePacksCount}");
            }
        }

        private int ProcessCount(int count)
        {
            count++;
            if ((count % 10) == 0)
            {
                Console.WriteLine($"{count} packs are processed");
            }
            return count;
        }

        private class ScanningStats
        {
            internal List<TemplatePackageSearchData> NewTemplatePacks { get; } = new List<TemplatePackageSearchData>();

            internal List<TemplatePackageSearchData> BecameTemplatePacks { get; } = new List<TemplatePackageSearchData>();

            internal List<TemplatePackageSearchData> RemovedTemplatePacks { get; } = new List<TemplatePackageSearchData>();

            internal List<(TemplatePackageSearchData Package, string OldVersion)> UpdatedTemplatePacks { get; } = new List<(TemplatePackageSearchData, string)>();

            internal int SameTemplatePacksCount { get; set; }

            internal int BecameNonTemplatePacksCount { get; set; }

            internal int NewNonTemplatePacksCount { get; set; }

            internal int UpdatedNonTemplatePacksCount { get; set; }

            internal int SameNonTemplatePacksCount { get; set; }

            internal int RemovedNonTemplatePacksCount { get; set; }

            internal int UnavailableNonTemplatePacksCount { get; set; }
        }
    }
}
