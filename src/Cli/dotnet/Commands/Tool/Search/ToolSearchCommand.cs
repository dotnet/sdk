// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.NugetSearch;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal sealed class ToolSearchCommand(
    ParseResult result,
    INugetToolSearchApiRequest nugetToolSearchApiRequest = null,
    string currentWorkingDirectory = null)
    : CommandBase<ToolSearchCommandDefinition>(result)
{
    private readonly INugetToolSearchApiRequest _nugetToolSearchApiRequest = nugetToolSearchApiRequest ?? new NugetToolSearchApiRequest();
    private readonly string _currentWorkingDirectory = currentWorkingDirectory;
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
        NugetSearchApiParameter nugetSearchApiParameter = GetNugetSearchApiParameter();
        int successCount = 0;

        foreach (PackageSource source in sourceConfiguration.PackageSources)
        {
            try
            {
                IReadOnlyCollection<SearchResultPackage> searchResultPackages =
                    _nugetToolSearchApiRequest.GetResult(nugetSearchApiParameter, source).GetAwaiter().GetResult();

                _searchResultPrinter.PrintSourceHeading(source);
                _searchResultPrinter.Print(isDetailed, searchResultPackages);
                successCount++;
            }
            catch (NugetSearchApiRequestException e)
            {
                _searchResultPrinter.PrintSourceFailure(source, e.Message);
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
}
