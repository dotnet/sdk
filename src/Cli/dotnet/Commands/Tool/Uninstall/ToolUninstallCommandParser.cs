// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal static class ToolUninstallCommandParser
{
    public static readonly CliArgument<string> PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

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
        CliCommand command = new("uninstall", LocalizableStrings.ToolUninstallCommandDescription);

        command.Arguments.Add(PackageIdArgument);
        command.Options.Add(GlobalOption.WithHelpDescription(command, LocalizableStrings.ToolUninstallGlobalOptionDescription));
        command.Options.Add(LocalOption.WithHelpDescription(command, LocalizableStrings.ToolUninstallLocalOptionDescription));
        command.Options.Add(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolUninstallToolPathOptionDescription));
        command.Options.Add(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ToolUninstallManifestPathOptionDescription));

        command.SetAction((parseResult) => new ToolUninstallCommand(parseResult).Execute());

        return command;
    }
}
