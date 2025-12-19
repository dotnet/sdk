// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

public static class InteractiveConsole
{
    /// <param name="acceptEscapeForFalse">
    /// If you need to confirm an action, Escape means "cancel" and that is fine.
    /// If you need an answer where the "no" might mean something dangerous, Escape should not be used as implicit "no".
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the user confirmed the action,
    /// <see langword="false"/> if the user declined it,
    /// <see langword="null"/> if the user could not answer because <c>--no-interactive</c> was specified.
    /// </returns>
    public static bool? Confirm(string message, ParseResult parseResult, bool acceptEscapeForFalse)
    {
        if (parseResult.GetValue(CommonOptions.YesOption))
        {
            return true;
        }

        if (!parseResult.GetValue<bool>(CommonOptions.InteractiveOptionName))
        {
            return null;
        }

        using var _ = Activities.Source.StartActivity("confirm-run-from-source");

        Console.Write(AddPromptOptions(message));

        while (true)
        {
            var key = Console.ReadKey();
            Console.WriteLine();

            if (key.Key == ConsoleKey.Enter || KeyMatches(key, CliCommandStrings.ConfirmationPromptYesValue))
            {
                return true;
            }

            if ((acceptEscapeForFalse && key.Key == ConsoleKey.Escape) || KeyMatches(key, CliCommandStrings.ConfirmationPromptNoValue))
            {
                return false;
            }

            Console.Write(AddPromptOptions(string.Format(CliCommandStrings.ConfirmationPromptInvalidChoiceMessage, CliCommandStrings.ConfirmationPromptYesValue, CliCommandStrings.ConfirmationPromptNoValue)));
        }

        static string AddPromptOptions(string message)
        {
            return $"{message} [{CliCommandStrings.ConfirmationPromptYesValue}/{CliCommandStrings.ConfirmationPromptNoValue}] ({CliCommandStrings.ConfirmationPromptYesValue}): ";
        }

        static bool KeyMatches(ConsoleKeyInfo pressedKey, string valueKey)
        {
            //  Apparently you can't do invariant case insensitive comparison on a char directly, so we have to convert it to a string.
            //  The resource string should be a single character, but we take the first character just to be sure.
            return pressedKey.KeyChar.ToString().ToLowerInvariant().Equals(
                valueKey.ToLowerInvariant().Substring(0, 1));
        }
    }

    public delegate bool Validator<TResult>(
        string? answer,
        out TResult? result,
        [NotNullWhen(returnValue: false)] out string? error);

    public static bool Ask<TResult>(
        string question,
        ParseResult parseResult,
        Validator<TResult> validate,
        out TResult? result)
    {
        if (!parseResult.GetValue<bool>(CommonOptions.InteractiveOptionName))
        {
            result = default;
            return false;
        }

        while (true)
        {
            Console.Write(question);
            Console.Write(' ');

            string? answer = Console.ReadLine();
            answer = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
            if (!validate(answer, out result, out var error))
            {
                Console.WriteLine(error);
            }
            else
            {
                return true;
            }
        }
    }
}
