// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable SA1202 // Elements should be ordered by access
#pragma warning disable SA1308 // Variable names should not be prefixed
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
#pragma warning disable CA1810 // Initialize reference type static fields inline
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli;

internal static class CommandDefinitionExtensions
{
    public static TDefinition AddShortNameArgumentValidator<TDefinition>(this TDefinition command, Argument nameArgument)
        where TDefinition : CommandDefinition
    {
        command.Validators.Add(commandResult =>
        {
            var nameArgumentResult = commandResult.Children.FirstOrDefault(symbol => symbol is ArgumentResult argumentResult && argumentResult.Argument == nameArgument);
            if (nameArgumentResult == null)
            {
                return;
            }

            ValidateArgumentUsage(commandResult, CommandDefinition.New.ShortNameArgument);
        });

        return command;
    }

    public static TDefinition AddNoLegacyUsageValidators<TDefinition>(this TDefinition command, params IEnumerable<Symbol> except)
        where TDefinition : CommandDefinition
    {
        foreach (var option in CommandDefinition.New.LegacyOptions)
        {
            if (!except.Contains(option))
            {
                command.Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(option));
            }
        }

        foreach (var argument in new Argument[] { CommandDefinition.New.ShortNameArgument, CommandDefinition.New.RemainingArguments })
        {
            if (!except.Contains(argument))
            {
                command.Validators.Add(symbolResult => symbolResult.ValidateArgumentUsage(argument));
            }
        }

        return command;
    }

    public static TDefinition AddOptions<TDefinition>(this TDefinition command, IEnumerable<Option> options)
        where TDefinition : CommandDefinition
    {
        command.Options.AddRange(options);
        return command;
    }

    internal static void ValidateArgumentUsage(this CommandResult commandResult, params Argument[] arguments)
    {
        if (commandResult.Parent is not CommandResult parentResult)
        {
            return;
        }

        List<string> wrongTokens = new();
        foreach (Argument argument in arguments)
        {
            var newCommandArgument = parentResult.Children.OfType<ArgumentResult>().FirstOrDefault(result => result.Argument == argument);
            if (newCommandArgument == null)
            {
                continue;
            }
            foreach (var token in newCommandArgument.Tokens)
            {
                if (!string.IsNullOrWhiteSpace(token.Value))
                {
                    wrongTokens.Add($"'{token.Value}'");
                }
            }
        }
        if (wrongTokens.Any())
        {
            //Unrecognized command or argument(s): {0}
            commandResult.AddError(string.Format(LocalizableStrings.Commands_Validator_WrongTokens, string.Join(",", wrongTokens)));
        }
    }

    internal static void ValidateOptionUsage(this CommandResult commandResult, Option option)
    {
        if (commandResult.Parent is not CommandResult parentResult)
        {
            return;
        }

        OptionResult? optionResult = parentResult.Children.OfType<OptionResult>().FirstOrDefault(result => result.Option == option);
        if (optionResult != null)
        {
            List<string> wrongTokens = new();
            if (optionResult.IdentifierToken is { } && !string.IsNullOrWhiteSpace(optionResult.IdentifierToken.Value))
            {
                wrongTokens.Add($"'{optionResult.IdentifierToken.Value}'");
            }
            foreach (var token in optionResult.Tokens)
            {
                if (token is { } t && !string.IsNullOrWhiteSpace(t.Value))
                {
                    wrongTokens.Add($"'{t.Value}'");
                }
            }
            //Unrecognized command or argument(s): {0}
            commandResult.AddError(string.Format(LocalizableStrings.Commands_Validator_WrongTokens, string.Join(",", wrongTokens)));
        }
    }
}
