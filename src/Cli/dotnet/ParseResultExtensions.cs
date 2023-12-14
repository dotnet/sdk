// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using static Microsoft.DotNet.Cli.Parser;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli
{
    public static class ParseResultExtensions
    {
        ///<summary>
        /// Finds the command of the parse result and invokes help for that command.
        /// If no command is specified, invokes help for the application.
        ///<summary>
        ///<remarks>
        /// This is accomplished by finding a set of tokens that should be valid and appending a help token
        /// to that list, then re-parsing the list of tokens. This is not ideal - either we should have a direct way
        /// of invoking help for a ParseResult, or we should eliminate this custom, ad-hoc help invocation by moving
        /// more situations that want to show help into Parsing Errors (which trigger help in the default System.CommandLine pipeline)
        /// or custom Invocation Middleware, so we can more easily create our version of a HelpResult type.
        ///</remarks>
        public static void ShowHelp(this ParseResult parseResult)
        {
            // take from the start of the list until we hit an option/--/unparsed token
            // since commands can have arguments, we must take those as well in order to get accurate help
            var tokenList = parseResult.Tokens.TakeWhile(token => token.Type == CliTokenType.Argument || token.Type == CliTokenType.Command || token.Type == CliTokenType.Directive).Select(t => t.Value).ToList();
            tokenList.Add("-h");
            Instance.Parse(tokenList).Invoke();
        }

        public static void ShowHelpOrErrorIfAppropriate(this ParseResult parseResult)
        {
            if (parseResult.Errors.Any())
            {
                var unrecognizedTokenErrors = parseResult.Errors.Where(error =>
                {
                    // Can't really cache this access in a static or something because it implicitly depends on the environment.
                    var rawResourcePartsForThisLocale = DistinctFormatStringParts(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument);
                    return ErrorContainsAllParts(error.Message, rawResourcePartsForThisLocale);
                });
                if (parseResult.CommandResult.Command.TreatUnmatchedTokensAsErrors ||
                    parseResult.Errors.Except(unrecognizedTokenErrors).Any())
                {
                    throw new CommandParsingException(
                        message: string.Join(Environment.NewLine,
                                             parseResult.Errors.Select(e => e.Message)),
                        parseResult: parseResult);
                }
            }

            ///<summary>Splits a .NET format string by the format placeholders (the {N} parts) to get an array of the literal parts, to be used in message-checking</summary> 
            static string[] DistinctFormatStringParts(string formatString)
            {
                return Regex.Split(formatString, @"{[0-9]+}"); // match the literal '{', followed by any of 0-9 one or more times, followed by the literal '}'
            }


            /// <summary>given a string and a series of parts, ensures that all parts are present in the string in sequential order</summary>
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
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public static string RootSubCommandResult(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Children?
                .Select(child => GetSymbolResultValue(parseResult, child))
                .FirstOrDefault(subcommand => !string.IsNullOrEmpty(subcommand)) ?? string.Empty;
        }

        public static bool IsDotnetBuiltInCommand(this ParseResult parseResult)
        {
            return string.IsNullOrEmpty(parseResult.RootSubCommandResult()) ||
                GetBuiltInCommand(parseResult.RootSubCommandResult()) != null;
        }

        public static bool IsTopLevelDotnetCommand(this ParseResult parseResult)
        {
            return parseResult.CommandResult.Command.Equals(RootCommand) && string.IsNullOrEmpty(parseResult.RootSubCommandResult());
        }

        public static bool CanBeInvoked(this ParseResult parseResult)
        {
            return GetBuiltInCommand(parseResult.RootSubCommandResult()) != null ||
                parseResult.Tokens.Any(token => token.Type == CliTokenType.Directive) ||
                (parseResult.IsTopLevelDotnetCommand() && string.IsNullOrEmpty(parseResult.GetValue(DotnetSubCommand)));
        }

        public static int HandleMissingCommand(this ParseResult parseResult)
        {
            Reporter.Error.WriteLine(Tools.CommonLocalizableStrings.RequiredCommandNotPassed.Red());
            parseResult.ShowHelp();
            return 1;
        }

        public static string[] GetArguments(this ParseResult parseResult)
        {
            return parseResult.Tokens.Select(t => t.Value)
                .ToArray()
                .GetSubArguments();
        }

        public static string[] GetSubArguments(this string[] args)
        {
            var subargs = args.ToList();

            // Don't remove any arguments that are being passed to the app in dotnet run
            var dashDashIndex = subargs.IndexOf("--");

            var runArgs = dashDashIndex > -1 ? subargs.GetRange(dashDashIndex, subargs.Count() - dashDashIndex) : new List<string>(0);
            subargs = dashDashIndex > -1 ? subargs.GetRange(0, dashDashIndex) : subargs;

            return subargs
                .SkipWhile(arg => DiagOption.Name.Equals(arg) || DiagOption.Aliases.Contains(arg) || arg.Equals("dotnet"))
                .Skip(1) // remove top level command (ex build or publish)
                .Concat(runArgs)
                .ToArray();
        }

        public static bool DiagOptionPrecedesSubcommand(this string[] args, string subCommand)
        {
            if (string.IsNullOrEmpty(subCommand))
            {
                return true;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(subCommand))
                {
                    return false;
                }
                else if (DiagOption.Name.Equals(args) || DiagOption.Aliases.Contains(args[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSymbolResultValue(ParseResult parseResult, SymbolResult symbolResult) => symbolResult switch
        {
            CommandResult commandResult => commandResult.Command.Name,
            ArgumentResult argResult => argResult.Tokens.FirstOrDefault()?.Value ?? string.Empty,
            _ => parseResult.GetResult(DotnetSubCommand)?.GetValueOrDefault<string>()
        };

        public static bool BothArchAndOsOptionsSpecified(this ParseResult parseResult) =>
            (parseResult.HasOption(CommonOptions.ArchitectureOption) ||
            parseResult.HasOption(CommonOptions.LongFormArchitectureOption)) &&
            parseResult.HasOption(CommonOptions.OperatingSystemOption);

        internal static string GetCommandLineRuntimeIdentifier(this ParseResult parseResult)
        {
            return parseResult.HasOption(RunCommandParser.RuntimeOption) ?
                parseResult.GetValue(RunCommandParser.RuntimeOption) :
                parseResult.HasOption(CommonOptions.OperatingSystemOption) ||
                parseResult.HasOption(CommonOptions.ArchitectureOption) ||
                parseResult.HasOption(CommonOptions.LongFormArchitectureOption) ?
                CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(
                    parseResult.GetValue(CommonOptions.OperatingSystemOption),
                    CommonOptions.ArchOptionValue(parseResult)) :
                null;
        }

        public static bool UsingRunCommandShorthandProjectOption(this ParseResult parseResult)
        {
            if (parseResult.HasOption(RunCommandParser.PropertyOption) && parseResult.GetValue(RunCommandParser.PropertyOption).Any())
            {
                var projVals = parseResult.GetRunCommandShorthandProjectValues();
                if (projVals.Any())
                {
                    if (projVals.Count() != 1 || parseResult.HasOption(RunCommandParser.ProjectOption))
                    {
                        throw new GracefulException(Tools.Run.LocalizableStrings.OnlyOneProjectAllowed);
                    }
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<string> GetRunCommandShorthandProjectValues(this ParseResult parseResult)
        {
            var properties = GetRunPropertyOptions(parseResult, true);
            return properties.Where(property => !property.Contains("="));
        }

        public static IEnumerable<string> GetRunCommandPropertyValues(this ParseResult parseResult)
        {
            var shorthandProperties = GetRunPropertyOptions(parseResult, true)
                .Where(property => property.Contains("="));
            var longhandProperties = GetRunPropertyOptions(parseResult, false);
            return longhandProperties.Concat(shorthandProperties);
        }

        private static IEnumerable<string> GetRunPropertyOptions(ParseResult parseResult, bool shorthand)
        {
            var optionString = shorthand ? "-p" : "--property";
            var propertyOptions = parseResult.CommandResult.Children.Where(c => GetOptionTokenOrDefault(c)?.Value.Equals(optionString) ?? false);
            var propertyValues = propertyOptions.SelectMany(o => o.Tokens.Select(t => t.Value)).ToArray();
            return propertyValues;

            static CliToken GetOptionTokenOrDefault(SymbolResult symbolResult)
            {
                if (symbolResult is not OptionResult optionResult)
                {
                    return null;
                }

                return optionResult.IdentifierToken ?? new CliToken($"--{optionResult.Option.Name}", CliTokenType.Option, optionResult.Option);
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

        /// <summary>
        /// Only returns the value for this option if the option is present and there are no parse errors for that option.
        /// This allows cross-cutting code like the telemetry filters to safely get the value without throwing on null-ref errors.
        /// If you are inside a command handler or 'normal' System.CommandLine code then you don't need this - the parse error handling
        /// will have covered these cases.
        /// </summary>
        public static T SafelyGetValueForOption<T>(this ParseResult parseResult, CliOption<T> optionToGet)
        {
            if (parseResult.GetResult(optionToGet) is OptionResult optionResult &&
                !parseResult.Errors.Any(e => e.SymbolResult == optionResult))
            {
                return optionResult.GetValue(optionToGet);
            }
            else
            {
                return default;
            }
        }

        public static bool HasOption(this ParseResult parseResult, CliOption option) => parseResult.GetResult(option) is not null;
    }
}
