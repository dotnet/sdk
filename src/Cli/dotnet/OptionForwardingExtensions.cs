// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Linq;
using Sprache;

namespace Microsoft.DotNet.Cli
{
    public static class OptionForwardingExtensions
    {
        public static ForwardedOption<T> Forward<T>(this ForwardedOption<T> option) => option.SetForwardingFunction((T o) => new string[] { option.Name });

        public static ForwardedOption<T> ForwardAs<T>(this ForwardedOption<T> option, string value) => option.SetForwardingFunction((T o) => new string[] { value });

        public static ForwardedOption<T> ForwardAsSingle<T>(this ForwardedOption<T> option, Func<T, string> format) => option.SetForwardingFunction(format);

        private static Parser<(string key, string value)[]> KeyValueParser {
            get {
                bool IsValidMSBuildPropertyChar (char c) => Char.IsLetterOrDigit(c) || (c == '_') || (c == '-');
            var keyParser = 
                Parse.Char(IsValidMSBuildPropertyChar, "Any alphanumeric character or '_'").AtLeastOnce().Text().Named("key");

                var unquotedValueParser = Parse.CharExcept('"').Named("unquoted value");
            
                var quoteParser = Parse.Char('"').Named("quote");
                var semiParser = Parse.Char(';').Named("tick");
                var quotedValueParser = 
                    Parse.Contained(unquotedValueParser.AtLeastOnce(), quoteParser, quoteParser).Text().Select(s => $"\"{s}\"").Named("quoted value");
                var valueParser = 
                    Parse.XOr(quotedValueParser, Parse.CharExcept(';').AtLeastOnce().Text()).Named("value");
                var keyValueParser = 
                    from key in keyParser
                    from _equals in Parse.Char('=')
                    from value in valueParser
                    select (key, value);
                var semiOrCommaParser = Parse.Chars(new[]{';', ','});
                var keyValues = Parse.DelimitedBy(keyValueParser.Named("key/value"), Parse.Char(';')).Select(pairs => pairs.ToArray()).Named("key/value list");
                return keyValues;
            }
        }
        public static IResult<(string key, string value)[]> ParseKeyValues(string msbuildKeyValue) => KeyValueParser.TryParse(msbuildKeyValue);

        public static ForwardedOption<string[]> ForwardAsProperty(this ForwardedOption<string[]> option) => option
            .SetForwardingFunction((optionVals) =>
                optionVals
                    .Select(ParseKeyValues)
                    .SelectMany(result => {
                        if (result.WasSuccessful && result.Remainder.AtEnd) {
                            return result.Value;
                        } else {
                            return Array.Empty<(string key, string value)>();
                        }
                    })
                    .Select(optionVal => (optionVal.key, value: optionVal.value.Replace(";", "%3B"))) // must escape semicolon-delimited property values when forwarding them to MSBuild
                    .Select(optionVal => $"{optionVal.key}={optionVal.value}")
                    .Select(optionVal => $"{option.Aliases.FirstOrDefault()}:{optionVal}")
                );

        public static Option<T> ForwardAsMany<T>(this ForwardedOption<T> option, Func<T, IEnumerable<string>> format) => option.SetForwardingFunction(format);

        public static Option<IEnumerable<string>> ForwardAsManyArgumentsEachPrefixedByOption(this ForwardedOption<IEnumerable<string>> option, string alias) => option.ForwardAsMany(o => ForwardedArguments(alias, o));

        public static IEnumerable<string> OptionValuesToBeForwarded(this ParseResult parseResult, Command command) =>
            command.Options
                .OfType<IForwardedOption>()
                .SelectMany(o => o.GetForwardingFunction()(parseResult)) ?? Array.Empty<string>();


        public static IEnumerable<string> ForwardedOptionValues<T>(this ParseResult parseResult, Command command, string alias) =>
            command.Options?
                .Where(o => o.Aliases.Contains(alias))?
                .OfType<IForwardedOption>()?
                .FirstOrDefault()?
                .GetForwardingFunction()(parseResult)
            ?? Array.Empty<string>();

        public static Option<T> AllowSingleArgPerToken<T>(this Option<T> option)
        {
            option.AllowMultipleArgumentsPerToken = false;
            return option;
        }

        public static Option<T> Hide<T>(this Option<T> option)
        {
            option.IsHidden = true;
            return option;
        }

        public static Option<T> WithHelpDescription<T>(this Option<T> option, Command command, string helpText)
        {
            if (Parser.HelpDescriptionCustomizations.ContainsKey(option))
            {
                Parser.HelpDescriptionCustomizations[option].Add(command, helpText);
            }
            else
            {
                Parser.HelpDescriptionCustomizations.Add(option, new Dictionary<Command, string>() { { command, helpText } });
            }

            return option;
        }

        private static IEnumerable<string> ForwardedArguments(string alias, IEnumerable<string> arguments)
        {
            foreach (string arg in arguments)
            {
                yield return alias;
                yield return arg;
            }
        }
    }

    public interface IForwardedOption
    {
        Func<ParseResult, IEnumerable<string>> GetForwardingFunction();
    }

    public class ForwardedOption<T> : Option<T>, IForwardedOption
    {
        private Func<ParseResult, IEnumerable<string>> ForwardingFunction;

        public ForwardedOption(string[] aliases, string description) : base(aliases, description) { }

        public ForwardedOption(string[] aliases) : base(aliases) { }

        public ForwardedOption(string alias, string description = null) : base(alias, description) { }

        public ForwardedOption(string alias, ParseArgument<T> parseArgument, string description = null) :
            base(alias, parseArgument, description: description) { }

        public ForwardedOption<T> SetForwardingFunction(Func<T, IEnumerable<string>> func)
        {
            ForwardingFunction = GetForwardingFunction(func);
            return this;
        }

        public ForwardedOption<T> SetForwardingFunction(Func<T, string> format)
        {
            ForwardingFunction = GetForwardingFunction((o) => new string[] { format(o) });
            return this;
        }

        public ForwardedOption<T> SetForwardingFunction(Func<T, ParseResult, IEnumerable<string>> func)
        {
            ForwardingFunction = (ParseResult parseResult) => parseResult.HasOption(this) ? func(parseResult.GetValueForOption<T>(this), parseResult) : Array.Empty<string>();
            return this;
        }

        public Func<ParseResult, IEnumerable<string>> GetForwardingFunction(Func<T, IEnumerable<string>> func)
        {
            return (ParseResult parseResult) => parseResult.HasOption(this) ? func(parseResult.GetValueForOption<T>(this)) : Array.Empty<string>();
        }

        public Func<ParseResult, IEnumerable<string>> GetForwardingFunction()
        {
            return ForwardingFunction;
        }
    }
}
