// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Common;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run;

internal static class ToolRunCommandParser
{
    public static readonly Argument<string> CommandNameArgument = new("commandName")
    {
        HelpName = CliCommandStrings.CommandNameArgumentName,
        Description = CliCommandStrings.CommandNameArgumentDescription
    };

    public static readonly Argument<IEnumerable<string>> CommandArgument = new("toolArguments")
    {
        Description = "arguments forwarded to the tool"
    };

    public static readonly Option<bool> RollForwardOption = new("--allow-roll-forward")
    {
        Description = CliCommandStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> FromSourceOption = new("--from-source")
    {
        Description = CliCommandStrings.ToolRunFromSourceOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> FromSourceConfigFile = ToolInstallCommandParser.ConfigOption;

    public static readonly Option<string[]> FromSourceSourceOption = ToolInstallCommandParser.SourceOption;

    public static readonly Option<string[]> FromSourceAddSourceOption = ToolInstallCommandParser.AddSourceOption;

    public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("run", CliCommandStrings.ToolRunCommandDescription);

        command.Arguments.Add(CommandNameArgument);
        command.Arguments.Add(CommandArgument);
        command.Options.Add(RollForwardOption);
        command.Options.Add(FromSourceOption);
        command.Options.Add(FromSourceConfigFile);
        command.Options.Add(FromSourceSourceOption);
        command.Options.Add(FromSourceAddSourceOption);
        command.Options.Add(VerbosityOption);

        command.Options.Add(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);

        command.SetAction((parseResult) => new ToolRunCommand(parseResult).Execute());

        return command;
    }
}
