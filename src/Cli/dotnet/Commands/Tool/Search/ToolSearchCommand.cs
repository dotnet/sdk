// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.NugetSearch;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal sealed class ToolSearchCommand(
    ParseResult result,
    INugetToolSearchApiRequest nugetToolSearchApiRequest = null)
    : CommandBase<ToolSearchCommandDefinition>(result)
{
    private readonly INugetToolSearchApiRequest _nugetToolSearchApiRequest = nugetToolSearchApiRequest ?? new NugetToolSearchApiRequest();
    private readonly SearchResultPrinter _searchResultPrinter = new(Reporter.Output);

    public override int Execute()
    {
        var isDetailed = _parseResult.GetValue(Definition.DetailOption);
        if (!PathUtility.CheckForNuGetInNuGetConfig())
        {
            Reporter.Output.WriteLine(CliCommandStrings.NeedNuGetInConfig);
            return 0;
        }

        IReadOnlyCollection<SearchResultPackage> searchResultPackages =
            NugetSearchApiResultDeserializer.Deserialize(
                _nugetToolSearchApiRequest.GetResult(GetNugetSearchApiParameter()).GetAwaiter().GetResult());

        _searchResultPrinter.Print(isDetailed, searchResultPackages);

        return 0;
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
