// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Remove;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Package;

internal static class RemovePackageCommandParser
{
    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new CliCommand("package", CliCommandStrings.PackageRemoveAppFullName);

        command.Arguments.Add(PackageRemoveCommandParser.CmdPackageArgument);
        command.Options.Add(PackageRemoveCommandParser.InteractiveOption);

        command.SetAction((parseResult) => new PackageRemoveCommand(parseResult).Execute());

        return command;
    }
}
