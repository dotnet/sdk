// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Restore;

internal static class ToolRestoreCommandParser
{
    public static readonly Option<string> ConfigOption = ToolRestoreCommandDefinition.ConfigOption;

    public static readonly Option<string[]> AddSourceOption = ToolRestoreCommandDefinition.AddSourceOption;

    public static readonly Option<string> ToolManifestOption = ToolRestoreCommandDefinition.ToolManifestOption;

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = ToolRestoreCommandDefinition.VerbosityOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = ToolRestoreCommandDefinition.Create();

        command.SetAction((parseResult) => new ToolRestoreCommand(parseResult).Execute());

        return command;
    }
}
