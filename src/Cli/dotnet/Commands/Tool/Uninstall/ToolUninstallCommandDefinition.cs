// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal static class ToolUninstallCommandDefinition
{
    public static readonly Argument<string> PackageIdArgument = new("packageId")
    {
        HelpName = "PACKAGE_ID",
        Description = CliStrings.PackageReference,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption(CliCommandStrings.ToolUninstallGlobalOptionDescription);

    public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption(CliCommandStrings.ToolUninstallLocalOptionDescription);

    public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption(CliCommandStrings.ToolUninstallToolPathOptionDescription);

    public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption(CliCommandStrings.ToolUninstallManifestPathOptionDescription);

    public static Command Create()
    {
        Command command = new("uninstall", CliCommandStrings.ToolUninstallCommandDescription);

        command.Arguments.Add(PackageIdArgument);
        command.Options.Add(GlobalOption);
        command.Options.Add(LocalOption);
        command.Options.Add(ToolPathOption);
        command.Options.Add(ToolManifestOption);

        return command;
    }
}
