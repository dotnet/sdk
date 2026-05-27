// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal static class AddCommandParser
{
    public static void ConfigureCommand(AddCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        PackageCommandParser.ConfigureAddCommand(command.PackageCommand);
        command.ReferenceCommand.SetAction(parseResult => new ReferenceAddCommand(parseResult).Execute());
    }
}
