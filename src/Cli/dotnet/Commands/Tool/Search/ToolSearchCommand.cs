// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.NugetSearch;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using NuGet.Credentials;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal sealed class ToolSearchCommand(
    ParseResult result,
    INugetToolSearchApiRequest nugetToolSearchApiRequest = null,
    string currentWorkingDirectory = null,
    Action<bool> setupCredentialService = null)
    : CommandBase<ToolSearchCommandDefinition>(result)
{
    private const int MaxConcurrentSourceRequests = 4;
    private readonly INugetToolSearchApiRequest _nugetToolSearchApiRequest = nugetToolSearchApiRequest ?? new NugetToolSearchApiRequest();
    private readonly string _currentWorkingDirectory = currentWorkingDirectory;
    private readonly Action<bool> _setupCredentialService = setupCredentialService
        ?? (interactive => DefaultCredentialServiceUtility.SetupDefaultCredentialService(new NuGetConsoleLogger(), !interactive));
    private readonly SearchResultPrinter _searchResultPrinter = new(Reporter.Output);

    public override int Execute()
    {
        var isDetailed = _parseResult.GetValue(Definition.DetailOption);

        NuGetSourceConfiguration sourceConfiguration = NuGetSourceConfiguration.Load(
            nugetConfig: _parseResult.GetValue(Definition.ConfigOption),
            sourceFeedOverrides: _parseResult.GetValue(Definition.SourceOption),
            additionalSourceFeeds: _parseResult.GetValue(Definition.AddSourceOption),
            basePath: _currentWorkingDirectory,
            invalidSource: _searchResultPrinter.PrintInvalidSource);

        if (sourceConfiguration.PackageSources.Count == 0)
        {
            _searchResultPrinter.PrintNoSourcesConfigured();
            return 1;
        }

        _setupCredentialService(_parseResult.GetValue(Definition.InteractiveOption));

        NugetSearchApiParameter nugetSearchApiParameter = GetNugetSearchApiParameter();
        using var concurrencyLimiter = new SemaphoreSlim(MaxConcurrentSourceRequests);
        SourceSearchResult[] results = Task.WhenAll(
            sourceConfiguration.PackageSources.Select(
                source => SearchSourceAsync(source, nugetSearchApiParameter, concurrencyLimiter)))
            .GetAwaiter()
            .GetResult();

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
            searchTerm: _parseResult.GetValue(Definition.SearchTermArgument),
            skip: GetParsedResultAsInt(Definition.SkipOption),
            take: GetParsedResultAsInt(Definition.TakeOption),
            prerelease: _parseResult.GetValue(Definition.PrereleaseOption));

    private async Task<SourceSearchResult> SearchSourceAsync(
        PackageSource source,
        NugetSearchApiParameter searchParameters,
        SemaphoreSlim concurrencyLimiter)
    {
        await concurrencyLimiter.WaitAsync().ConfigureAwait(false);
        try
        {
            IReadOnlyCollection<SearchResultPackage> packages =
                await _nugetToolSearchApiRequest.GetResult(searchParameters, source).ConfigureAwait(false);
            return new SourceSearchResult(source, packages, ErrorMessage: null);
        }
        catch (NugetSearchApiRequestException e)
        {
            return new SourceSearchResult(source, Packages: null, e.Message);
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
        string ErrorMessage);
}
