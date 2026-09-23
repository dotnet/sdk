// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Self;

/// <summary>Registers executable maintenance separately from SDK and runtime updates.</summary>
internal static class SelfCommandParser
{
    internal static Option<string?> ChannelOption { get; } = CreateChannelOption();

    public static Command GetCommand()
    {
        var command = new Command("self", Strings.SelfCommandDescription);
        var update = new Command("update", Strings.SelfUpdateCommandDescription);
        update.Options.Add(ChannelOption);
        update.Options.Add(CommonOptions.NoProgressOption);
        update.SetAction(result => new SelfUpdateCommand(result).Execute());
        command.Subcommands.Add(update);
        return command;
    }

    private static Option<string?> CreateChannelOption()
    {
        // No default: SelfUpdateCommand derives the channel from the running build when omitted.
        var option = new Option<string?>("--channel")
        {
            Description = Strings.SelfUpdateChannelOptionDescription,
        };
        option.AcceptOnlyFromAmong("daily", "preview", "stable");
        return option;
    }
}