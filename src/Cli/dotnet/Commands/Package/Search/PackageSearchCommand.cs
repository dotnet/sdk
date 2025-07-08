// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Extensions;
using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.Search;

internal class PackageSearchCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    public override int Execute()
    {
        var args = new List<string>
        {
            "package",
            "search"
        };

        var searchArgument = _parseResult.GetValue(PackageSearchCommandParser.SearchTermArgument);
        if (searchArgument != null)
        {
            args.Add(searchArgument);
        }

        args.AddRange(_parseResult.OptionValuesToBeForwarded(PackageSearchCommandParser.GetCommand()));
        return NuGetCommand.Run([.. args]);
    }
}
