// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.CommandLine;
using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.Search;

internal sealed class PackageSearchCommand(ParseResult parseResult)
    : CommandBase<PackageSearchCommandDefinition>(parseResult)
{
    public override int Execute()
    {
        var args = new List<string>
        {
            "package",
            "search"
        };

        var searchArgument = _parseResult.GetValue(Definition.SearchTermArgument);
        if (searchArgument != null)
        {
            args.Add(searchArgument);
        }

        args.AddRange(_parseResult.OptionValuesToBeForwarded(Definition));
        return NuGetCommand.Run([.. args]);
    }
}
