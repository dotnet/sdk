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
    internal readonly record struct PackageAvailabilityCandidate(string PackageId, string PackageVersion);

    internal sealed record PackageAvailabilityResult(
        IReadOnlySet<PackageAvailabilityCandidate> AvailablePackages,
        bool AnyFeedSucceeded);

    internal sealed class PackageAvailabilityChecker
    {
        private const int MaxConcurrentPackageChecks = 4;
        private const string FeedResourceUnavailableMessage = "Unable to query NuGet source '{0}' for template package availability: the feed does not support package lookups.";
        private const string FeedQueryFailedMessageFormat = "Unable to query NuGet source '{0}' for template package availability: {1}";

        private readonly IReadOnlyList<PackageSource> _sources;
        private readonly PackageSourceMapping? _sourceMapping;
        private readonly ILogger _logger;
        private readonly Action<string> _reportFeedFailure;
        private readonly Func<PackageSource, CancellationToken, Task<FindPackageByIdResource?>> _resourceFactory;

        internal PackageAvailabilityChecker(
            IReadOnlyList<PackageSource> sources,
            PackageSourceMapping? sourceMapping = null,
            ILogger? logger = null,
            Action<string>? reportFeedFailure = null,
            Func<PackageSource, CancellationToken, Task<FindPackageByIdResource?>>? resourceFactory = null)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            _sourceMapping = sourceMapping;
            _logger = logger ?? NullLogger.Instance;
            _reportFeedFailure = reportFeedFailure ?? (message => Reporter.Error.WriteLine(message));
            _resourceFactory = resourceFactory ?? ((source, token) =>
                Repository.Factory.GetCoreV3(source).GetResourceAsync<FindPackageByIdResource>(token));
        }

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

        internal async Task<PackageAvailabilityResult> GetAvailablePackagesAsync(
            IReadOnlyCollection<PackageAvailabilityCandidate> candidates,
            CancellationToken cancellationToken)
        {
            if (_sources.Count == 0)
            {
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), AnyFeedSucceeded: false);
            }

            Dictionary<string, List<ParsedCandidate>> candidatesByPackageId = ParseAndGroupCandidates(candidates);
            if (candidatesByPackageId.Count == 0)
            {
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), AnyFeedSucceeded: true);
            }

            using SemaphoreSlim throttle = new(MaxConcurrentPackageChecks, MaxConcurrentPackageChecks);
            FeedResult?[] results = await Task.WhenAll(
                _sources.Select(source => CheckFeedAsync(source, candidatesByPackageId, throttle, cancellationToken)))
                .ConfigureAwait(false);
            FeedResult[] queriedResults = results.OfType<FeedResult>().ToArray();
            if (queriedResults.Length == 0)
            {
                return new PackageAvailabilityResult(new HashSet<PackageAvailabilityCandidate>(), AnyFeedSucceeded: true);
            }

            ReportFeedFailures(queriedResults);

            return new PackageAvailabilityResult(
                queriedResults.SelectMany(result => result.AvailablePackages).ToHashSet(),
                queriedResults.Any(result => result.Succeeded));
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

        private async Task<FeedResult?> CheckFeedAsync(
            PackageSource source,
            Dictionary<string, List<ParsedCandidate>> candidatesByPackageId,
            SemaphoreSlim throttle,
            CancellationToken cancellationToken)
        {
            Dictionary<string, List<ParsedCandidate>> eligibleCandidates = candidatesByPackageId
                .Where(package => _sourceMapping == null || _sourceMapping
                    .GetConfiguredPackageSources(package.Key)
                    .Contains(source.Name, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(package => package.Key, package => package.Value, StringComparer.OrdinalIgnoreCase);

            if (eligibleCandidates.Count == 0)
            {
                return null;
            }

            FindPackageByIdResource? resource;
            try
            {
                resource = await RunThrottledAsync(
                    throttle,
                    () => _resourceFactory(source, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new FeedResult(source, [], Succeeded: false, ex, ResourceUnavailable: false);
            }

            if (resource == null)
            {
                return new FeedResult(source, [], Succeeded: false, Exception: null, ResourceUnavailable: true);
            }

            QueryResult[] results = await Task.WhenAll(
                eligibleCandidates.Select(package =>
                    QueryPackageAsync(resource, package.Key, package.Value, throttle, cancellationToken)))
                .ConfigureAwait(false);

            return new FeedResult(
                source,
                results.SelectMany(result => result.AvailablePackages),
                results.Any(result => result.Succeeded),
                results.FirstOrDefault(result => result.Exception != null)?.Exception,
                ResourceUnavailable: false);
        }

        private async Task<QueryResult> QueryPackageAsync(
            FindPackageByIdResource resource,
            string packageId,
            IReadOnlyList<ParsedCandidate> packageCandidates,
            SemaphoreSlim throttle,
            CancellationToken cancellationToken)
        {
            try
            {
                IEnumerable<NuGetVersion> versions = await RunThrottledAsync(
                    throttle,
                    async () =>
                    {
                        using SourceCacheContext cacheContext = new();
                        return await resource.GetAllVersionsAsync(packageId, cacheContext, _logger, cancellationToken)
                            .ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);
                HashSet<NuGetVersion> availableVersions = new(versions, VersionComparer.VersionRelease);
                return new QueryResult(
                    packageCandidates
                        .Where(candidate => availableVersions.Contains(candidate.Version))
                        .Select(candidate => candidate.Candidate),
                    Succeeded: true,
                    Exception: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new QueryResult([], Succeeded: false, ex);
            }
        }

        private static async Task<T> RunThrottledAsync<T>(
            SemaphoreSlim throttle,
            Func<Task<T>> action,
            CancellationToken cancellationToken)
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                throttle.Release();
            }
        }

        private void ReportFeedFailures(IEnumerable<FeedResult> results)
        {
            foreach (FeedResult result in results)
            {
                string source = GetSourceDisplayName(result.Source);
                if (result.ResourceUnavailable)
                {
                    _reportFeedFailure(string.Format(FeedResourceUnavailableMessage, source));
                }
                else if (result.Exception != null)
                {
                    _reportFeedFailure(string.Format(FeedQueryFailedMessageFormat, source, result.Exception.Message));
                    Reporter.Verbose.WriteLine(result.Exception.ToString());
                }
            }
        }

        private static string GetSourceDisplayName(PackageSource source) =>
            string.IsNullOrEmpty(source.Name) ? source.Source : source.Name;

        private sealed record ParsedCandidate(PackageAvailabilityCandidate Candidate, NuGetVersion Version);

        private sealed record QueryResult(
            IEnumerable<PackageAvailabilityCandidate> AvailablePackages,
            bool Succeeded,
            Exception? Exception);

        private sealed record FeedResult(
            PackageSource Source,
            IEnumerable<PackageAvailabilityCandidate> AvailablePackages,
            bool Succeeded,
            Exception? Exception,
            bool ResourceUnavailable);
    }
}
