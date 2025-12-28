// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run;

internal sealed class ToolRunCommandDefinition : Command
{
    public readonly Argument<string> CommandNameArgument = new("commandName")
    {
        HelpName = CliCommandStrings.CommandNameArgumentName,
        Description = CliCommandStrings.CommandNameArgumentDescription
    };

    public readonly Argument<IEnumerable<string>> CommandArgument = new("toolArguments")
    {
        Description = CliCommandStrings.ToolRunArgumentsDescription
    };

    public readonly Option<bool> RollForwardOption = new("--allow-roll-forward")
    {
        Description = CliCommandStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public ToolRunCommandDefinition()
        : base("run", CliCommandStrings.ToolRunCommandDescription)
    {
        Arguments.Add(CommandNameArgument);
        Arguments.Add(CommandArgument);
        Options.Add(RollForwardOption);
    }
}
