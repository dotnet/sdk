// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List;

internal static class ListCommandParser
{
    private static readonly Command Command = SetAction(new ListCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(ListCommandDefinition def)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());

        def.PackageCommand.SetAction(parseResult => new PackageListCommand(parseResult).Execute());
        def.ReferenceCommand.SetAction(parseResult => new ReferenceListCommand(parseResult).Execute());

        return def;
    }
}
