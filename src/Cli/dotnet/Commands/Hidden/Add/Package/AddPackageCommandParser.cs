// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Package.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;

internal static class AddPackageCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("package", CliCommandStrings.PackageAddAppFullName);

        command.Arguments.Add(PackageAddCommandParser.CmdPackageArgument);
        command.Options.Add(PackageAddCommandParser.VersionOption);
        command.Options.Add(PackageAddCommandParser.FrameworkOption);
        command.Options.Add(PackageAddCommandParser.NoRestoreOption);
        command.Options.Add(PackageAddCommandParser.SourceOption);
        command.Options.Add(PackageAddCommandParser.PackageDirOption);
        command.Options.Add(PackageAddCommandParser.InteractiveOption);
        command.Options.Add(PackageAddCommandParser.PrereleaseOption);
        command.Options.Add(PackageCommandParser.ProjectOption);
        command.Options.Add(PackageCommandParser.FileOption);

        command.SetAction((parseResult) => new PackageAddCommand(parseResult).Execute());

        return command;
    }
}
