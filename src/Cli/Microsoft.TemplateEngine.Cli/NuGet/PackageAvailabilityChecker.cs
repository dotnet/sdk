// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        /// <see langword="true"/> when at least one required package query completed successfully (even if it
        /// reported no matching packages at all); <see langword="false"/> when none of the selected feeds were usable.
        /// When source mapping excludes every candidate, no query is required and the result is also successful.
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
        private const int MaxConcurrentPackageChecks = 4;
        private const string FeedResourceUnavailableMessage = "Unable to query NuGet source '{0}' for template package availability: the feed does not support package lookups.";
        private const string FeedQueryFailedMessageFormat = "Unable to query NuGet source '{0}' for template package availability: {1}";

        private readonly IReadOnlyList<PackageSource> _sources;
        private readonly PackageSourceMapping? _sourceMapping;
        private readonly ILogger _logger;
        private readonly Action<string> _reportFeedFailure;
        private readonly Func<PackageSource, SourceRepository> _repositoryFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="PackageAvailabilityChecker"/>.
        /// </summary>
        /// <param name="sources">The NuGet feeds selected for this invocation.</param>
        /// <param name="sourceMapping">The effective configured package source mapping, or <see langword="null"/> when mapping is disabled or bypassed by source overrides.</param>
        /// <param name="logger">The NuGet <see cref="ILogger"/> to use for feed queries. Defaults to <see cref="NullLogger.Instance"/>.</param>
        /// <param name="reportFeedFailure">Callback invoked with a human readable message when an individual feed cannot be queried. Defaults to writing to <see cref="Reporter.Error"/>.</param>
        /// <param name="repositoryFactory">Factory used to create a <see cref="SourceRepository"/> for a given <see cref="PackageSource"/>. Defaults to <see cref="Repository.Factory"/>. Overridable for testing.</param>
        internal PackageAvailabilityChecker(
            IReadOnlyList<PackageSource> sources,
            PackageSourceMapping? sourceMapping = null,
            ILogger? logger = null,
            Action<string>? reportFeedFailure = null,
            Func<PackageSource, SourceRepository>? repositoryFactory = null)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            _sourceMapping = sourceMapping;
            _logger = logger ?? NullLogger.Instance;
            _reportFeedFailure = reportFeedFailure ?? (message => Reporter.Error.WriteLine(message));
            _repositoryFactory = repositoryFactory ?? ((PackageSource source) => Repository.Factory.GetCoreV3(source));
        }

        /// <summary>
        /// Resolves the package source mapping policy used by template search.
        /// </summary>
        internal static PackageSourceMapping? GetEffectivePackageSourceMapping(
            ISettings settings,
            bool sourceOverridesSpecified,
            bool additionalSourcesSpecified)
        {
            if (sourceOverridesSpecified)
            {
                return null;
            }

            PackageSourceMapping sourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
            if (!sourceMapping.IsEnabled)
            {
                return null;
            }

            if (additionalSourcesSpecified)
            {
                throw new GracefulException(LocalizableStrings.CannotUseAddSourceWithSourceMapping);
            }

            return sourceMapping;
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

            Dictionary<string, List<ParsedCandidate>> candidatesByPackageId = ParseAndGroupCandidates(candidates);
            if (candidatesByPackageId.Count == 0)
            {
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), anyFeedSucceeded: true);
            }

            List<FeedWork> feedWork = CreateFeedWork(candidatesByPackageId);
            if (feedWork.Count == 0)
            {
                // Package source mapping excluded every candidate, so no feed query was required.
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), anyFeedSucceeded: true);
            }

            using SemaphoreSlim throttle = new(MaxConcurrentPackageChecks, MaxConcurrentPackageChecks);
            FeedResourceResult[] feedResources = await Task.WhenAll(
                feedWork.Select(work => ResolveResourceAsync(work, throttle, cancellationToken))).ConfigureAwait(false);

            QueryResult[] queryResults = await Task.WhenAll(
                feedResources
                    .Where(result => result.Resource != null)
                    .SelectMany(result => result.Work.CandidatesByPackageId.Select(package =>
                        QueryPackageAsync(result.Work.Source, result.Resource!, package.Key, package.Value, throttle, cancellationToken))))
                .ConfigureAwait(false);

            ReportFeedFailures(feedResources, queryResults);

            HashSet<PackageAvailabilityCandidate> availablePackages = queryResults
                .Where(result => result.AvailableCandidates != null)
                .SelectMany(result => result.AvailableCandidates!)
                .ToHashSet();

            return new PackageAvailabilityResult(
                availablePackages,
                anyFeedSucceeded: queryResults.Any(result => result.Succeeded));
        }

        private static Dictionary<string, List<ParsedCandidate>> ParseAndGroupCandidates(
            IReadOnlyCollection<PackageAvailabilityCandidate> candidates)
        {
            Dictionary<string, List<ParsedCandidate>> candidatesByPackageId = new(StringComparer.OrdinalIgnoreCase);
            foreach (PackageAvailabilityCandidate candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate.PackageId) ||
                    !NuGetVersion.TryParse(candidate.PackageVersion, out NuGetVersion? version))
                {
                    continue;
                }

                if (!candidatesByPackageId.TryGetValue(candidate.PackageId, out List<ParsedCandidate>? packageCandidates))
                {
                    packageCandidates = [];
                    candidatesByPackageId.Add(candidate.PackageId, packageCandidates);
                }

                packageCandidates.Add(new ParsedCandidate(candidate, version));
            }

            return candidatesByPackageId;
        }

        private List<FeedWork> CreateFeedWork(Dictionary<string, List<ParsedCandidate>> candidatesByPackageId)
        {
            List<FeedWork> work = [];
            foreach (PackageSource source in _sources)
            {
                IReadOnlyDictionary<string, List<ParsedCandidate>> eligibleCandidates = _sourceMapping == null
                    ? candidatesByPackageId
                    : candidatesByPackageId
                        .Where(package => _sourceMapping
                            .GetConfiguredPackageSources(package.Key)
                            .Contains(source.Name, StringComparer.OrdinalIgnoreCase))
                        .ToDictionary(package => package.Key, package => package.Value, StringComparer.OrdinalIgnoreCase);

                if (eligibleCandidates.Count > 0)
                {
                    work.Add(new FeedWork(source, eligibleCandidates));
                }
            }

            return work;
        }

        private async Task<FeedResourceResult> ResolveResourceAsync(
            FeedWork work,
            SemaphoreSlim throttle,
            CancellationToken cancellationToken)
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SourceRepository repository = _repositoryFactory(work.Source);
                FindPackageByIdResource? resource =
                    await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
                return new FeedResourceResult(work, resource, ResourceUnavailable: resource == null, Exception: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new FeedResourceResult(work, Resource: null, ResourceUnavailable: false, ex);
            }
            finally
            {
                throttle.Release();
            }
        }

        private async Task<QueryResult> QueryPackageAsync(
            PackageSource source,
            FindPackageByIdResource resource,
            string packageId,
            IReadOnlyList<ParsedCandidate> packageCandidates,
            SemaphoreSlim throttle,
            CancellationToken cancellationToken)
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using SourceCacheContext cacheContext = new();
                IEnumerable<NuGetVersion> versions = await resource
                    .GetAllVersionsAsync(packageId, cacheContext, _logger, cancellationToken)
                    .ConfigureAwait(false);
                HashSet<NuGetVersion> availableVersions = new(versions, VersionComparer.VersionRelease);
                IReadOnlyList<PackageAvailabilityCandidate> availableCandidates = packageCandidates
                    .Where(candidate => availableVersions.Contains(candidate.Version))
                    .Select(candidate => candidate.Candidate)
                    .ToList();
                return new QueryResult(source, Succeeded: true, availableCandidates, Exception: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new QueryResult(source, Succeeded: false, AvailableCandidates: null, ex);
            }
            finally
            {
                throttle.Release();
            }
        }

        private void ReportFeedFailures(
            IReadOnlyList<FeedResourceResult> feedResources,
            IReadOnlyList<QueryResult> queryResults)
        {
            foreach (PackageSource source in _sources)
            {
                FeedResourceResult? feedResource = feedResources.FirstOrDefault(result => ReferenceEquals(result.Work.Source, source));
                if (feedResource == null)
                {
                    continue;
                }

                if (feedResource.ResourceUnavailable)
                {
                    _reportFeedFailure(string.Format(FeedResourceUnavailableMessage, GetSourceDisplayName(source)));
                    continue;
                }

                Exception? exception = feedResource.Exception
                    ?? queryResults.FirstOrDefault(result => ReferenceEquals(result.Source, source) && result.Exception != null)?.Exception;
                if (exception != null)
                {
                    _reportFeedFailure(string.Format(FeedQueryFailedMessageFormat, GetSourceDisplayName(source), exception.Message));
                    Reporter.Verbose.WriteLine(exception.ToString());
                }
            }
        }

        private static string GetSourceDisplayName(PackageSource source) =>
            string.IsNullOrEmpty(source.Name) ? source.Source : source.Name;

        private sealed record ParsedCandidate(PackageAvailabilityCandidate Candidate, NuGetVersion Version);

        private sealed record FeedWork(
            PackageSource Source,
            IReadOnlyDictionary<string, List<ParsedCandidate>> CandidatesByPackageId);

        private sealed record FeedResourceResult(
            FeedWork Work,
            FindPackageByIdResource? Resource,
            bool ResourceUnavailable,
            Exception? Exception);

        private sealed record QueryResult(
            PackageSource Source,
            bool Succeeded,
            IReadOnlyList<PackageAvailabilityCandidate>? AvailableCandidates,
            Exception? Exception);
    }
}
