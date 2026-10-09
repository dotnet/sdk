// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Self;

/// <summary>Registers executable maintenance separately from SDK and runtime updates.</summary>
internal static class SelfCommandParser
{
    internal static Option<string?> ChannelOption { get; } = CreateChannelOption();

    internal static Option<bool> ForceOption { get; } = new("--force")
    {
        Description = Strings.SelfUpdateForceOptionDescription,
    };

    internal static Option<bool> UpdateNotificationsOption { get; } = new("--update-notifications")
    {
        Description = Strings.SelfUpdateNotificationsOptionDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public static Command GetCommand()
    {
        var command = new Command("self", Strings.SelfCommandDescription);
        var update = new Command("update", Strings.SelfUpdateCommandDescription);
        update.Options.Add(ChannelOption);
        update.Options.Add(ForceOption);
        update.Options.Add(UpdateNotificationsOption);
        update.Options.Add(CommonOptions.NoProgressOption);
        update.Validators.Add(static result =>
        {
            // --update-notifications only changes a setting, so options that shape an update would be ignored.
            if (IsExplicit(result.GetResult(UpdateNotificationsOption)) &&
                (IsExplicit(result.GetResult(ChannelOption)) || IsExplicit(result.GetResult(ForceOption))))
            {
                result.AddError(Strings.SelfUpdateNotificationsConflict);
            }
        });
        update.SetAction(result => new SelfUpdateCommand(result).Execute());
        command.Subcommands.Add(update);
        return command;
    }

    // Boolean options receive an implicit result, so presence alone does not mean the user passed them.
    private static bool IsExplicit(OptionResult? result) => result is { Implicit: false };

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