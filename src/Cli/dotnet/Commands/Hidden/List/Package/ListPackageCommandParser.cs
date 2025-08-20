// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.List;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List.Package;

internal static class ListPackageCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("package", CliCommandStrings.PackageListAppFullName);

        command.Options.Add(PackageListCommandParser.VerbosityOption);
        command.Options.Add(PackageListCommandParser.OutdatedOption);
        command.Options.Add(PackageListCommandParser.DeprecatedOption);
        command.Options.Add(PackageListCommandParser.VulnerableOption);
        command.Options.Add(PackageListCommandParser.FrameworkOption);
        command.Options.Add(PackageListCommandParser.TransitiveOption);
        command.Options.Add(PackageListCommandParser.PrereleaseOption);
        command.Options.Add(PackageListCommandParser.HighestPatchOption);
        command.Options.Add(PackageListCommandParser.HighestMinorOption);
        command.Options.Add(PackageListCommandParser.ConfigOption);
        command.Options.Add(PackageListCommandParser.SourceOption);
        command.Options.Add(PackageListCommandParser.InteractiveOption);
        command.Options.Add(PackageListCommandParser.FormatOption);
        command.Options.Add(PackageListCommandParser.OutputVersionOption);
        command.Options.Add(PackageListCommandParser.NoRestore);

        command.SetAction((parseResult) => new PackageListCommand(parseResult).Execute());

        return command;
    }
}
