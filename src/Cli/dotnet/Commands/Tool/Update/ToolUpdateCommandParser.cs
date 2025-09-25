// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;

namespace Microsoft.DotNet.Cli.Commands.Tool.Update;

internal static class ToolUpdateCommandParser
{
    public static readonly Argument<PackageIdentityWithRange?> PackageIdentityArgument = CommonArguments.OptionalPackageIdentityArgument(CliStrings.ToolPackageIdentityArgumentDescription);

    public static readonly Option<bool> UpdateAllOption = ToolAppliedOption.UpdateAllOption;

    public static readonly Option<bool> AllowPackageDowngradeOption = ToolInstallCommandParser.AllowPackageDowngradeOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("update", CliCommandStrings.ToolUpdateCommandDescription);

        command.Arguments.Add(PackageIdentityArgument);

        ToolInstallCommandParser.AddCommandOptions(command);
        command.Options.Add(AllowPackageDowngradeOption);
        command.Options.Add(UpdateAllOption);

        command.SetAction((parseResult) => new ToolUpdateCommand(parseResult).Execute());

        return command;
    }
}
