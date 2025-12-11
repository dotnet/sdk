// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Install;

namespace Microsoft.DotNet.Cli.Commands.Tool.Execute;

internal static class ToolExecuteCommandParser
{
    public static readonly Argument<PackageIdentityWithRange> PackageIdentityArgument = ToolExecuteCommandDefinition.PackageIdentityArgument;

    public static readonly Argument<IEnumerable<string>> CommandArgument = ToolExecuteCommandDefinition.CommandArgument;

    public static readonly Option<string> VersionOption = ToolExecuteCommandDefinition.VersionOption;
    public static readonly Option<bool> RollForwardOption = ToolExecuteCommandDefinition.RollForwardOption;
    public static readonly Option<bool> PrereleaseOption = ToolExecuteCommandDefinition.PrereleaseOption;
    public static readonly Option<string> ConfigOption = ToolExecuteCommandDefinition.ConfigOption;
    public static readonly Option<string[]> SourceOption = ToolExecuteCommandDefinition.SourceOption;
    public static readonly Option<string[]> AddSourceOption = ToolExecuteCommandDefinition.AddSourceOption;
    public static readonly Option<bool> InteractiveOption = ToolExecuteCommandDefinition.InteractiveOption;
    public static readonly Option<bool> YesOption = ToolExecuteCommandDefinition.YesOption;
    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = ToolExecuteCommandDefinition.VerbosityOption;

    public static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = ToolExecuteCommandDefinition.Create();

        command.SetAction((parseResult) => new ToolExecuteCommand(parseResult).Execute());

        return command;
    }
}
