// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.ToolPackage;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NugetSearch;

internal sealed class NugetToolSearchApiRequest : INugetToolSearchApiRequest
{
    private readonly Func<PackageSource, CancellationToken, Task<PackageSearchResource?>> _getPackageSearchResource;

    public NugetToolSearchApiRequest()
        : this(GetPackageSearchResource)
    {
    }

    internal NugetToolSearchApiRequest(
        Func<PackageSource, CancellationToken, Task<PackageSearchResource?>> getPackageSearchResource)
    {
        _getPackageSearchResource = getPackageSearchResource;
    }

    public async Task<IReadOnlyCollection<SearchResultPackage>> GetResult(
        NugetSearchApiParameter nugetSearchApiParameter,
        PackageSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nugetSearchApiParameter);
        ArgumentNullException.ThrowIfNull(source);

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            PackageSearchResource? searchResource = await _getPackageSearchResource(source, timeoutCancellation.Token).ConfigureAwait(false);
            if (searchResource is null)
            {
                throw new NugetSearchApiRequestException(
                    $"The source '{source.Source}' does not provide {nameof(PackageSearchResource)}.");
            }

            if (!searchResource.SupportsPackageTypeFiltering)
            {
                throw new NugetSearchApiRequestException(
                    string.Format(CliCommandStrings.ToolSearchSourceDoesNotSupportPackageTypeFiltering, source.Source));
            }

            var searchFilter = new SearchFilter(nugetSearchApiParameter.Prerelease)
            {
                PackageType = PackageType.DotnetTool.Name,
            };

            IEnumerable<IPackageSearchMetadata> metadata = await searchResource.SearchAsync(
                nugetSearchApiParameter.SearchTerm ?? string.Empty,
                searchFilter,
                nugetSearchApiParameter.Skip ?? 0,
                nugetSearchApiParameter.Take ?? 20,
                NullLogger.Instance,
                timeoutCancellation.Token).ConfigureAwait(false);

            var results = new List<SearchResultPackage>();
            foreach (IPackageSearchMetadata packageMetadata in metadata)
            {
                IEnumerable<VersionInfo> versions = await packageMetadata.GetVersionsAsync().ConfigureAwait(false);
                results.Add(new SearchResultPackage(
                    new PackageId(packageMetadata.Identity.Id),
                    packageMetadata.Identity.Version.ToNormalizedString(),
                    packageMetadata.Description,
                    packageMetadata.Summary,
                    SplitMetadataField(packageMetadata.Tags, splitOnSpaces: true),
                    SplitMetadataField(packageMetadata.Authors, splitOnSpaces: false),
                    packageMetadata.DownloadCount ?? 0,
                    packageMetadata.PrefixReserved,
                    versions.Select(version => new SearchResultPackageVersion(
                        version.Version.ToNormalizedString(),
                        version.DownloadCount ?? 0)).ToArray()));
            }

            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException
            or NuGetProtocolException
            or IOException
            or InvalidOperationException
            or NotSupportedException)
        {
            throw new NugetSearchApiRequestException(ex.Message);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NugetSearchApiRequestException(ex.Message);
        }
    }

    private static async Task<PackageSearchResource?> GetPackageSearchResource(
        PackageSource source,
        CancellationToken cancellationToken)
        => await Repository.Factory.GetCoreV3(source)
            .GetResourceAsync<PackageSearchResource>(cancellationToken)
            .ConfigureAwait(false);

    private static string[] SplitMetadataField(string? value, bool splitOnSpaces)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                splitOnSpaces ? [',', ' '] : [','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
