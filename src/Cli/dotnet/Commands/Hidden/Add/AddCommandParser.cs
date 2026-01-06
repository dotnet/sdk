// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal static class AddCommandParser
{
    private static readonly Command Command = SetActionAndCompletions(new AddCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetActionAndCompletions(AddCommandDefinition def)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());

        PackageCommandParser.ConfigureAddCommand(def.PackageCommand);
        def.ReferenceCommand.SetAction(parseResult => new ReferenceAddCommand(parseResult).Execute());
        return def;
    }
}
