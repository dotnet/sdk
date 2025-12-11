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
    public static readonly Argument<string> PackageIdArgument = ToolUninstallCommandDefinition.PackageIdArgument;

    public static readonly Option<bool> GlobalOption = ToolUninstallCommandDefinition.GlobalOption;

    public static readonly Option<bool> LocalOption = ToolUninstallCommandDefinition.LocalOption;

    public static readonly Option<string> ToolPathOption = ToolUninstallCommandDefinition.ToolPathOption;

    public static readonly Option<string> ToolManifestOption = ToolUninstallCommandDefinition.ToolManifestOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = ToolUninstallCommandDefinition.Create();

        command.SetAction((parseResult) => new ToolUninstallCommand(parseResult).Execute());

        return command;
    }
}
