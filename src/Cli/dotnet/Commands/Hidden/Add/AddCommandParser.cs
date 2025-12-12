// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal static class AddCommandParser
{
    private static readonly Command Command = SetAction(new AddCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(AddCommandDefinition def)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());

        def.PackageCommand.SetAction(parseResult => new PackageAddCommand(parseResult).Execute());
        def.ReferenceCommand.SetAction(parseResult => new ReferenceAddCommand(parseResult).Execute());
        return def;
    }
}
