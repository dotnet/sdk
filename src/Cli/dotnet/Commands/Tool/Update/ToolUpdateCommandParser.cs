// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.Commands.Tool.Update;

internal static class ToolUpdateCommandParser
{
    public static readonly CliArgument<PackageIdentity?> PackageIdentityArgument = CommonArguments.PackageIdentityArgument(requireArgument: false);

    public static readonly CliOption<bool> UpdateAllOption = ToolAppliedOption.UpdateAllOption;

    public static readonly CliOption<bool> AllowPackageDowngradeOption = ToolInstallCommandParser.AllowPackageDowngradeOption;

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("update", CliCommandStrings.ToolUpdateCommandDescription);

        command.Arguments.Add(PackageIdentityArgument);

        ToolInstallCommandParser.AddCommandOptions(command);
        command.Options.Add(AllowPackageDowngradeOption);
        command.Options.Add(UpdateAllOption);

        command.SetAction((parseResult) => new ToolUpdateCommand(parseResult).Execute());

        return command;
    }
}
