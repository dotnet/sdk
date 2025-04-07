// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal static class ToolUninstallCommandParser
{
    public static readonly CliArgument<string> PackageIdArgument = new("packageId")
    {
        HelpName = "PACKAGE_ID",
        Description = CommonLocalizableStrings.PackageReference,
    };

    public static readonly CliOption<bool> GlobalOption = ToolAppliedOption.GlobalOption;

    public static readonly CliOption<bool> LocalOption = ToolAppliedOption.LocalOption;

    public static readonly CliOption<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

    public static readonly CliOption<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("uninstall", CliCommandStrings.ToolUninstallCommandDescription);

        command.Arguments.Add(PackageIdArgument);
        command.Options.Add(GlobalOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallGlobalOptionDescription));
        command.Options.Add(LocalOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallLocalOptionDescription));
        command.Options.Add(ToolPathOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallToolPathOptionDescription));
        command.Options.Add(ToolManifestOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallManifestPathOptionDescription));

        command.SetAction((parseResult) => new ToolUninstallCommand(parseResult).Execute());

        return command;
    }
}
