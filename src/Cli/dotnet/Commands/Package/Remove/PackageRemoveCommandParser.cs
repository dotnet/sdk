// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Package.Remove;

internal static class PackageRemoveCommandParser
{
    public static readonly Argument<string[]> CmdPackageArgument = new(CliCommandStrings.CmdPackage)
    {
        Description = CliCommandStrings.PackageRemoveAppHelpText,
        Arity = ArgumentArity.OneOrMore,
    };

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("remove", CliCommandStrings.PackageRemoveAppFullName);

        command.Arguments.Add(CmdPackageArgument);
        command.Options.Add(InteractiveOption);
        command.Options.Add(PackageCommandParser.ProjectOption);
        command.Options.Add(PackageCommandParser.FileOption);

        command.SetAction((parseResult) => new PackageRemoveCommand(parseResult).Execute());

        return command;
    }
}
