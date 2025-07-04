﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli;

public static class InteractiveConsole
{
    /// <param name="acceptEscapeForFalse">
    /// If you need to confirm an action, Escape means "cancel" and that is fine.
    /// If you need an answer where the "no" might mean something dangerous, Escape should not be used as implicit "no".
    /// </param>
    public static bool? Confirm(string message, ParseResult parseResult, bool acceptEscapeForFalse)
    {
        if (parseResult.GetValue(CommonOptions.YesOption))
        {
            return true;
        }

        if (!parseResult.GetValue(CommonOptions.InteractiveOption()))
        {
            return null;
        }

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

    public static string? Ask(string question, ParseResult parseResult, Func<string?, string?> validate)
    {
        if (!parseResult.GetValue(CommonOptions.InteractiveOption()))
        {
            return null;
        }

        while (true)
        {
            Console.Write(question);
            Console.Write(' ');

            string? answer = Console.ReadLine();
            answer = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
            if (validate(answer) is { } error)
            {
                Console.WriteLine(error);
            }
            else
            {
                return answer;
            }
        }
    }
}
