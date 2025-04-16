// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Remove;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Package;

internal static class RemovePackageCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("package", CliCommandStrings.PackageRemoveAppFullName);

        command.Arguments.Add(PackageRemoveCommandParser.CmdPackageArgument);
        command.Options.Add(PackageRemoveCommandParser.InteractiveOption);

        command.SetAction((parseResult) => new PackageRemoveCommand(parseResult).Execute());

        return command;
    }
}
