// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Cli.NuGet
{
    /// <summary>
    /// Identifies a specific package id + version pair, as discovered from the .NET template catalog, that needs to be
    /// confirmed as actually available from one of the currently selected NuGet feeds before it is shown to the user.
    /// </summary>
    internal readonly record struct PackageAvailabilityCandidate(string PackageId, string PackageVersion);

    /// <summary>
    /// The result of checking a set of <see cref="PackageAvailabilityCandidate"/> instances against the configured NuGet feeds.
    /// </summary>
    internal sealed class PackageAvailabilityResult
    {
        internal PackageAvailabilityResult(IReadOnlySet<PackageAvailabilityCandidate> availablePackages, bool anyFeedSucceeded)
        {
            AvailablePackages = availablePackages;
            AnyFeedSucceeded = anyFeedSucceeded;
        }

        /// <summary>
        /// The subset of the requested candidates that were confirmed to be available (matching package id and version)
        /// from at least one of the selected NuGet feeds.
        /// </summary>
        internal IReadOnlySet<PackageAvailabilityCandidate> AvailablePackages { get; }

        /// <summary>
        /// <see langword="true"/> when at least one of the selected feeds could be queried successfully (even if it
        /// reported no matching packages at all); <see langword="false"/> when none of the selected feeds were usable.
        /// </summary>
        internal bool AnyFeedSucceeded { get; }
    }

    /// <summary>
    /// Confirms which package id + version pairs discovered via the .NET template catalog are actually available from
    /// the NuGet feeds selected for a <c>dotnet new search</c> invocation (the feeds configured via NuGet.config,
    /// narrowed/overridden by <c>--configfile</c>, <c>--source</c>, and <c>--add-source</c>).
    /// This allows replacement or proxy feeds to filter out catalog entries that are not (or are no longer) available
    /// from any reachable feed. It intentionally does not attempt to discover packages that are absent from the
    /// catalog altogether - it can only narrow catalog results, not add to them.
    /// </summary>
    internal sealed class PackageAvailabilityChecker
    {
        // Bounds the number of concurrent per-candidate feed queries so a large set of catalog matches cannot open
        // an unbounded number of connections against the selected feeds.
        private const int MaxConcurrentPackageChecks = 4;

        // These are plain (non-localized) diagnostic messages: they are written only to stderr/verbose output to help
        // diagnose why a particular feed could not be used, and are distinct from the "no NuGet sources configured"
        // and "invalid --source/--add-source value" cases (both of which reuse existing localized strings at the
        // call site in the coordinator).
        private const string FeedResourceUnavailableMessage = "Unable to query NuGet source '{0}' for template package availability: the feed does not support package lookups.";
        private const string FeedQueryFailedMessageFormat = "Unable to query NuGet source '{0}' for template package availability: {1}";

        private readonly IReadOnlyList<PackageSource> _sources;
        private readonly ISettings? _settings;
        private readonly ILogger _logger;
        private readonly Action<string> _reportFeedFailure;
        private readonly Func<PackageSource, SourceRepository> _repositoryFactory;
        private readonly SourceCacheContext _cacheContext = new()
        {
            NoCache = true,
            DirectDownload = true
        };

        /// <summary>
        /// Initializes a new instance of <see cref="PackageAvailabilityChecker"/>.
        /// </summary>
        /// <param name="sources">The NuGet feeds selected for this invocation.</param>
        /// <param name="settings">The effective NuGet <see cref="ISettings"/>, used to resolve package source mapping. May be <see langword="null"/> if unavailable, in which case source mapping is not honored.</param>
        /// <param name="logger">The NuGet <see cref="ILogger"/> to use for feed queries. Defaults to <see cref="NullLogger.Instance"/>.</param>
        /// <param name="reportFeedFailure">Callback invoked with a human readable message whenever an individual feed cannot be queried. Defaults to writing to <see cref="Reporter.Error"/>.</param>
        /// <param name="repositoryFactory">Factory used to create a <see cref="SourceRepository"/> for a given <see cref="PackageSource"/>. Defaults to <see cref="Repository.Factory"/>. Overridable for testing.</param>
        internal PackageAvailabilityChecker(
            IReadOnlyList<PackageSource> sources,
            ISettings? settings = null,
            ILogger? logger = null,
            Action<string>? reportFeedFailure = null,
            Func<PackageSource, SourceRepository>? repositoryFactory = null)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            _settings = settings;
            _logger = logger ?? NullLogger.Instance;
            _reportFeedFailure = reportFeedFailure ?? (message => Reporter.Error.WriteLine(message));
            _repositoryFactory = repositoryFactory ?? ((PackageSource source) => Repository.Factory.GetCoreV3(source));
        }

        /// <summary>
        /// Checks which of the given <paramref name="candidates"/> are available from at least one of the selected NuGet feeds.
        /// </summary>
        internal async Task<PackageAvailabilityResult> GetAvailablePackagesAsync(
            IReadOnlyCollection<PackageAvailabilityCandidate> candidates,
            CancellationToken cancellationToken)
        {
            if (_sources.Count == 0)
            {
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), anyFeedSucceeded: false);
            }

            if (candidates.Count == 0)
            {
                // Nothing to confirm: there is no package to fail to find, so this is a vacuous success rather than
                // a feed failure. No feed needs to be queried.
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), anyFeedSucceeded: true);
            }

            List<(PackageSource Source, FindPackageByIdResource Resource)> usableFeeds = new();
            foreach (PackageSource source in _sources)
            {
                FindPackageByIdResource? resource = await TryGetResourceAsync(source, cancellationToken).ConfigureAwait(false);
                if (resource != null)
                {
                    usableFeeds.Add((source, resource));
                }
            }

            if (usableFeeds.Count == 0)
            {
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), anyFeedSucceeded: false);
            }

            PackageSourceMapping? sourceMapping = TryGetPackageSourceMapping();

            ConcurrentDictionary<PackageAvailabilityCandidate, byte> available = new();
            using SemaphoreSlim throttle = new(MaxConcurrentPackageChecks, MaxConcurrentPackageChecks);

            await Task.WhenAll(candidates.Select(candidate =>
                CheckCandidateAsync(candidate, usableFeeds, sourceMapping, available, throttle, cancellationToken))).ConfigureAwait(false);

            return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(available.Keys), anyFeedSucceeded: true);
        }

        private async Task CheckCandidateAsync(
            PackageAvailabilityCandidate candidate,
            IReadOnlyList<(PackageSource Source, FindPackageByIdResource Resource)> usableFeeds,
            PackageSourceMapping? sourceMapping,
            ConcurrentDictionary<PackageAvailabilityCandidate, byte> available,
            SemaphoreSlim throttle,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(candidate.PackageId) || !NuGetVersion.TryParse(candidate.PackageVersion, out NuGetVersion? version))
            {
                // Cannot confirm availability without a parseable version: treat as unavailable instead of failing the whole search.
                return;
            }

            IReadOnlyList<(PackageSource Source, FindPackageByIdResource Resource)> eligibleFeeds =
                GetEligibleFeeds(usableFeeds, sourceMapping, candidate.PackageId);
            if (eligibleFeeds.Count == 0)
            {
                return;
            }

            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach ((PackageSource source, FindPackageByIdResource resource) in eligibleFeeds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        bool exists = await resource.DoesPackageExistAsync(candidate.PackageId, version, _cacheContext, _logger, cancellationToken)
                            .ConfigureAwait(false);
                        if (exists)
                        {
                            available.TryAdd(candidate, 0);
                            return;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        ReportFailure(source, ex);
                    }
                }
            }
            finally
            {
                throttle.Release();
            }
        }

        private async Task<FindPackageByIdResource?> TryGetResourceAsync(PackageSource source, CancellationToken cancellationToken)
        {
            try
            {
                SourceRepository repository = _repositoryFactory(source);
                FindPackageByIdResource? resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
                if (resource == null)
                {
                    _reportFeedFailure(string.Format(FeedResourceUnavailableMessage, GetSourceDisplayName(source)));
                }
                return resource;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ReportFailure(source, ex);
                return null;
            }
        }

        private void ReportFailure(PackageSource source, Exception ex)
        {
            _reportFeedFailure(string.Format(FeedQueryFailedMessageFormat, GetSourceDisplayName(source), ex.Message));
            Reporter.Verbose.WriteLine(ex.ToString());
        }

        private PackageSourceMapping? TryGetPackageSourceMapping()
        {
            if (_settings == null)
            {
                return null;
            }

            try
            {
                PackageSourceMapping mapping = PackageSourceMapping.GetPackageSourceMapping(_settings);
                return mapping.IsEnabled ? mapping : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Reporter.Verbose.WriteLine(ex.ToString());
                return null;
            }
        }

        private static IReadOnlyList<(PackageSource Source, FindPackageByIdResource Resource)> GetEligibleFeeds(
            IReadOnlyList<(PackageSource Source, FindPackageByIdResource Resource)> usableFeeds,
            PackageSourceMapping? sourceMapping,
            string packageId)
        {
            if (sourceMapping == null)
            {
                return usableFeeds;
            }

            IReadOnlyList<string> matchedSourceNames = sourceMapping.GetConfiguredPackageSources(packageId);
            if (matchedSourceNames.Count == 0)
            {
                // Package source mapping is enabled but no pattern matches this package id: mirror real NuGet restore
                // semantics and treat the package as unavailable from any source, rather than falling back to all feeds.
                return Array.Empty<(PackageSource Source, FindPackageByIdResource Resource)>();
            }

            HashSet<string> matched = new(matchedSourceNames, StringComparer.OrdinalIgnoreCase);
            return usableFeeds.Where(feed => matched.Contains(feed.Source.Name)).ToList();
        }

        private static string GetSourceDisplayName(PackageSource source) =>
            string.IsNullOrEmpty(source.Name) ? source.Source : source.Name;
    }
}
