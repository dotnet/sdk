// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal static class ToolUninstallCommandParser
{
    public static readonly Argument<string> PackageIdArgument = new("packageId")
    {
        HelpName = "PACKAGE_ID",
        Description = CliStrings.PackageReference,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption;

    public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption;

    public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

    public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("uninstall", CliCommandStrings.ToolUninstallCommandDescription);

        command.Arguments.Add(PackageIdArgument);
        command.Options.Add(GlobalOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallGlobalOptionDescription));
        command.Options.Add(LocalOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallLocalOptionDescription));
        command.Options.Add(ToolPathOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallToolPathOptionDescription));
        command.Options.Add(ToolManifestOption.WithHelpDescription(command, CliCommandStrings.ToolUninstallManifestPathOptionDescription));

        command.SetAction((parseResult) => new ToolUninstallCommand(parseResult).Execute());

        return command;
    }
}
