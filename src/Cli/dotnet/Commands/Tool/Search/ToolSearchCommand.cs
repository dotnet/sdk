// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.NugetSearch;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using NuGet.Credentials;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal sealed class ToolSearchCommand
{
    private const int MaxConcurrentSourceRequests = 4;
    private readonly ParseResult _parseResult;
    private readonly ToolSearchCommandDefinition _definition;
    private readonly INugetToolSearchApiRequest _nugetToolSearchApiRequest;
    private readonly string? _currentWorkingDirectory;
    private readonly Action<bool> _setupCredentialService;
    private readonly SearchResultPrinter _searchResultPrinter;

    public ToolSearchCommand(
        ParseResult result,
        INugetToolSearchApiRequest? nugetToolSearchApiRequest = null,
        string? currentWorkingDirectory = null,
        Action<bool>? setupCredentialService = null)
    {
        result.ShowHelpOrErrorIfAppropriate();
        _parseResult = result;
        _definition = (ToolSearchCommandDefinition)result.CommandResult.Command;
        _nugetToolSearchApiRequest = nugetToolSearchApiRequest ?? new NugetToolSearchApiRequest();
        _currentWorkingDirectory = currentWorkingDirectory;
        _setupCredentialService = setupCredentialService
            ?? (interactive => DefaultCredentialServiceUtility.SetupDefaultCredentialService(new NuGetConsoleLogger(), !interactive));
        _searchResultPrinter = new SearchResultPrinter(Reporter.Output);
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var isDetailed = _parseResult.GetValue(_definition.DetailOption);

        NuGetSourceConfiguration sourceConfiguration = NuGetSourceConfiguration.Load(
            nugetConfig: _parseResult.GetValue(_definition.ConfigOption)?.FullName,
            sourceFeedOverrides: _parseResult.GetValue(_definition.SourceOption),
            additionalSourceFeeds: _parseResult.GetValue(_definition.AddSourceOption),
            basePath: _currentWorkingDirectory,
            invalidSource: _searchResultPrinter.PrintInvalidSource);

        if (sourceConfiguration.PackageSources.Count == 0)
        {
            _searchResultPrinter.PrintNoSourcesConfigured();
            return 1;
        }

        _setupCredentialService(_parseResult.GetValue(_definition.InteractiveOption));

        NugetSearchApiParameter nugetSearchApiParameter = GetNugetSearchApiParameter();
        using var concurrencyLimiter = new SemaphoreSlim(MaxConcurrentSourceRequests);
        SourceSearchResult[] results = await Task.WhenAll(
            sourceConfiguration.PackageSources.Select(
                source => SearchSourceAsync(source, nugetSearchApiParameter, concurrencyLimiter, cancellationToken)))
            .ConfigureAwait(false);

        int successCount = 0;
        foreach (SourceSearchResult result in results)
        {
            if (result.ErrorMessage is null)
            {
                _searchResultPrinter.PrintSourceHeading(result.Source);
                _searchResultPrinter.Print(isDetailed, result.Packages);
                successCount++;
            }
            else
            {
                _searchResultPrinter.PrintSourceFailure(result.Source, result.ErrorMessage);
            }
        }

        return successCount > 0 ? 0 : 1;
    }

    internal NugetSearchApiParameter GetNugetSearchApiParameter()
        => new(
            searchTerm: _parseResult.GetValue(_definition.SearchTermArgument),
            skip: GetParsedResultAsInt(_definition.SkipOption),
            take: GetParsedResultAsInt(_definition.TakeOption),
            prerelease: _parseResult.GetValue(_definition.PrereleaseOption));

    private async Task<SourceSearchResult> SearchSourceAsync(
        PackageSource source,
        NugetSearchApiParameter searchParameters,
        SemaphoreSlim concurrencyLimiter,
        CancellationToken cancellationToken)
    {
        await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyCollection<SearchResultPackage> packages =
                await _nugetToolSearchApiRequest.GetResult(searchParameters, source, cancellationToken).ConfigureAwait(false);
            return new SourceSearchResult(source, packages, ErrorMessage: null);
        }
        catch (NugetSearchApiRequestException e)
        {
            return new SourceSearchResult(source, Packages: [], e.Message);
        }
        finally
        {
            concurrencyLimiter.Release();
        }
    }

    private int? GetParsedResultAsInt(Option<string> alias)
    {
        var valueFromParser = _parseResult.GetValue(alias);
        if (string.IsNullOrWhiteSpace(valueFromParser))
        {
            return null;
        }

        if (int.TryParse(valueFromParser, out int i))
        {
            return i;
        }
        else
        {
            throw new GracefulException(
                string.Format(
                    CliStrings.InvalidInputTypeInteger,
                    alias));
        }
    }

    private sealed record SourceSearchResult(
        PackageSource Source,
        IReadOnlyCollection<SearchResultPackage> Packages,
        string? ErrorMessage);
}
