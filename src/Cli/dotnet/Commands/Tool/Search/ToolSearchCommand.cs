// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.NugetSearch;
using Microsoft.DotNet.Cli.Utils;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal class ToolSearchCommand(
    ParseResult result,
    INugetToolSearchApiRequest nugetToolSearchApiRequest = null
    ) : CommandBase(result)
{
    private readonly INugetToolSearchApiRequest _nugetToolSearchApiRequest = nugetToolSearchApiRequest ?? new NugetToolSearchApiRequest();
    private readonly SearchResultPrinter _searchResultPrinter = new SearchResultPrinter(Reporter.Output);

    public override int Execute()
    {
        var isDetailed = _parseResult.GetValue(ToolSearchCommandParser.DetailOption);
        if (!PathUtility.CheckForNuGetInNuGetConfig())
        {
            Reporter.Output.WriteLine(LocalizableStrings.NeedNuGetInConfig);
            return 0;
        }

        NugetSearchApiParameter nugetSearchApiParameter = new(_parseResult);
        IReadOnlyCollection<SearchResultPackage> searchResultPackages =
            NugetSearchApiResultDeserializer.Deserialize(
                _nugetToolSearchApiRequest.GetResult(nugetSearchApiParameter).GetAwaiter().GetResult());

        _searchResultPrinter.Print(isDetailed, searchResultPackages);

        return 0;
    }
}
