// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli.Extensions;

public static class ParseResultExtensions
{
    /// <summary>
    /// Finds the command of the parse result and invokes help for that command.
    /// If no command is specified, invokes help for the application.
    /// </summary>
    /// <remarks>
    /// This is accomplished by finding a set of tokens that should be valid and appending a help token
    /// to that list, then re-parsing the list of tokens. This is not ideal - either we should have a direct way
    /// of invoking help for a ParseResult, or we should eliminate this custom, ad-hoc help invocation by moving
    /// more situations that want to show help into Parsing Errors (which trigger help in the default System.CommandLine pipeline)
    /// or custom Invocation Middleware, so we can more easily create our version of a HelpResult type.
    /// </remarks>
    public static void ShowHelp(this ParseResult parseResult)
    {
        // Take from the start of the list until we hit an option/--/unparsed token.
        // Since commands can have arguments, we must take those as well in order to get accurate help.
        var filteredTokenValues = parseResult.Tokens.TakeWhile(token =>
            token.Type == TokenType.Argument
                || token.Type == TokenType.Command
                || token.Type == TokenType.Directive)
            .Select(t => t.Value);
        Parser.Parse([.. filteredTokenValues, "-h"]).Invoke();
    }

    public static void ShowHelpOrErrorIfAppropriate(this ParseResult parseResult)
    {
        if (parseResult.Errors.Any())
        {
            var unrecognizedTokenErrors = parseResult.Errors.Where(error =>
            {
                // Can't really cache this access in a static or something because it implicitly depends on the environment.
                var rawResourcePartsForThisLocale = DistinctFormatStringParts(CliStrings.UnrecognizedCommandOrArgument);
                return ErrorContainsAllParts(error.Message, rawResourcePartsForThisLocale);
            });

            if (parseResult.CommandResult.Command.TreatUnmatchedTokensAsErrors
                || parseResult.Errors.Except(unrecognizedTokenErrors).Any())
            {
                throw new CommandParsingException(
                    message: string.Join(Environment.NewLine, parseResult.Errors.Select(e => e.Message)),
                    parseResult: parseResult);
            }
        }

        /// <summary>
        /// Splits a .NET format string by the format placeholders (the {N} parts) to get an array of the literal parts, to be used in message-checking.
        /// </summary>
        static string[] DistinctFormatStringParts(string formatString) =>
            // Match the literal '{', followed by any of 0-9 one or more times, followed by the literal '}'.
            Regex.Split(formatString, @"{[0-9]+}");

        /// <summary>
        /// Given a string and a series of parts, ensures that all parts are present in the string in sequential order.
        /// </summary>
        static bool ErrorContainsAllParts(ReadOnlySpan<char> error, string[] parts)
        {
            foreach (var part in parts)
            {
                var foundIndex = error.IndexOf(part);
                if (foundIndex != -1)
                {
                    error = error.Slice(foundIndex + part.Length);
                    continue;
                }

                return false;
            }

            return true;
        }
    }

    public static string RootSubCommandResult(this ParseResult parseResult) => parseResult.RootCommandResult.Children?
        .Select(child => parseResult.GetSymbolResultValue(child))
        .FirstOrDefault(subcommand => !string.IsNullOrEmpty(subcommand)) ?? string.Empty;

    public static bool IsDotnetBuiltInCommand(this ParseResult parseResult) =>
        string.IsNullOrEmpty(parseResult.RootSubCommandResult())
        || Parser.GetBuiltInCommand(parseResult.RootSubCommandResult()) != null;

    public static bool IsTopLevelDotnetCommand(this ParseResult parseResult) =>
        parseResult.CommandResult.Command.Equals(Parser.RootCommand) && string.IsNullOrEmpty(parseResult.RootSubCommandResult());

    public static bool CanBeInvoked(this ParseResult parseResult) =>
        Parser.GetBuiltInCommand(parseResult.RootSubCommandResult()) != null
        || parseResult.Tokens.Any(token => token.Type == TokenType.Directive)
        || (parseResult.IsTopLevelDotnetCommand() && string.IsNullOrEmpty(parseResult.GetValue(Parser.RootCommand.DotnetSubCommand)));

    public static int HandleMissingCommand(this ParseResult parseResult)
    {
        Reporter.Error.WriteLine(CliStrings.RequiredCommandNotPassed.Red());
        parseResult.ShowHelp();
        return 1;
    }

    public static string[] GetArguments(this ParseResult parseResult) =>
        parseResult.Tokens.Select(t => t.Value).ToArray().GetSubArguments();

    public static string[] GetSubArguments(this string[] args)
    {
        var subargs = args.ToList();

        // Don't remove any arguments that are being passed to the app in dotnet run
        var dashDashIndex = subargs.IndexOf("--");

        var runArgs = dashDashIndex > -1 ? subargs.GetRange(dashDashIndex, subargs.Count() - dashDashIndex) : [];
        subargs = dashDashIndex > -1 ? subargs.GetRange(0, dashDashIndex) : subargs;

        // Remove top level command (ex build or publish).
        var subargsFiltered = subargs
            .SkipWhile(arg => Parser.RootCommand.DiagOption.Name.Equals(arg)
                || Parser.RootCommand.DiagOption.Aliases.Contains(arg)
                || arg.Equals("dotnet"))
            .Skip(1);

        return [.. subargsFiltered, .. runArgs];
    }

    private static string? GetSymbolResultValue(this ParseResult parseResult, SymbolResult symbolResult) => symbolResult switch
    {
        CommandResult commandResult => commandResult.Command.Name,
        ArgumentResult argResult => argResult.Tokens.FirstOrDefault()?.Value,
        _ => parseResult.GetResult(Parser.RootCommand.DotnetSubCommand)?.GetValueOrDefault<string>()
    };

    public static IEnumerable<string>? GetRunCommandShorthandProjectValues(this ParseResult parseResult) =>
        parseResult.GetRunPropertyOptions(true)?.Where(property => !property.Contains("="));

    public static IEnumerable<string> GetRunCommandPropertyValues(this ParseResult parseResult)
    {
        var shorthandProperties = parseResult.GetRunPropertyOptions(true)?.Where(property => property.Contains("="));
        var longhandProperties = parseResult.GetRunPropertyOptions(false);
        return (shorthandProperties, longhandProperties) switch
        {
            (null, null) => Enumerable.Empty<string>(),
            (null, var longhand) => longhand,
            (var shorthand, null) => shorthand,
            (var shorthand, var longhand) => shorthand.Concat(longhand)
        };
    }

    private static IEnumerable<string>? GetRunPropertyOptions(this ParseResult parseResult, bool shorthand)
    {
        var optionString = shorthand ? "-p" : "--property";
        var propertyOptions = parseResult.CommandResult.Children.Where(c => GetOptionTokenOrDefault(c)?.Value.Equals(optionString) ?? false);
        var propertyValues = propertyOptions.SelectMany(o => o.Tokens.Select(t => t.Value)).ToArray();
        return propertyValues;

        static Token? GetOptionTokenOrDefault(SymbolResult symbolResult)
        {
            if (symbolResult is not OptionResult optionResult)
            {
                return null;
            }

            return optionResult.IdentifierToken ?? new Token($"--{optionResult.Option.Name}", TokenType.Option, optionResult.Option);
        }
    }

    [Conditional("DEBUG")]
    public static void HandleDebugSwitch(this ParseResult parseResult)
    {
        if (parseResult.HasOption(CommonOptions.DebugOption))
        {
            DebugHelper.WaitForDebugger();
        }
    }

    public static string GetCommandName(this ParseResult parseResult)
    {
        // Walk the parent command tree to find the top-level command name and get the full command name for this ParseResult.
        List<string> parentNames = [parseResult.CommandResult.Command.Name];
        var current = parseResult.CommandResult.Parent;
        while (current is CommandResult parentCommandResult)
        {
            parentNames.Add(parentCommandResult.Command.Name);
            current = parentCommandResult.Parent;
        }
        parentNames.Reverse();

        // Options that perform terminating actions are considered part of the command name as they are essentially subcommands themselves.
        // Example: dotnet --version
        if (parseResult.Action is InvocableOptionAction { Terminating: true } optionAction)
        {
            parentNames.Add(optionAction.Option.Name);
        }

        return string.Join(' ', parentNames);
    }
}
