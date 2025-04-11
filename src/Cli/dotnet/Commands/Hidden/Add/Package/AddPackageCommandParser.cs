// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;

internal static class AddPackageCommandParser
{
    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("package", CliCommandStrings.PackageAddAppFullName);

        command.Arguments.Add(PackageAddCommandParser.CmdPackageArgument);
        command.Options.Add(PackageAddCommandParser.VersionOption);
        command.Options.Add(PackageAddCommandParser.FrameworkOption);
        command.Options.Add(PackageAddCommandParser.NoRestoreOption);
        command.Options.Add(PackageAddCommandParser.SourceOption);
        command.Options.Add(PackageAddCommandParser.PackageDirOption);
        command.Options.Add(PackageAddCommandParser.InteractiveOption);
        command.Options.Add(PackageAddCommandParser.PrereleaseOption);
        command.Options.Add(PackageCommandParser.ProjectOption);

        command.SetAction((parseResult) =>
        {
            // this command can be called with an argument or an option for the project path - we prefer the option.
            // if the option is not present, we use the argument value instead.
            if (parseResult.HasOption(PackageCommandParser.ProjectOption))
            {
                return new PackageAddCommand(parseResult, parseResult.GetValue(PackageCommandParser.ProjectOption)).Execute();
            }
            else
            {
                return new PackageAddCommand(parseResult, parseResult.GetValue(AddCommandParser.ProjectArgument)).Execute();
            }
        });

        return command;
    }
}
