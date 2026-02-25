// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// Extensions for tracking and invoking forwarding functions on options and arguments.
/// Forwarding functions are used to translate the parsed value of an option or argument
/// into a set of zero or more string values that will be passed to an inner command.
/// </summary>
public static class ForwardedOptionExtensions
{
    private static readonly Dictionary<Symbol, Func<ParseResult, IEnumerable<string>>> s_forwardingFunctions = [];
    private static readonly Lock s_lock = new();

    extension(Option option)
    {
        /// <summary>
        /// If this option has a forwarding function, this property will return it; otherwise, it will be null.
        /// </summary>
        /// <remarks>
        /// This getter is on the untyped Option because much of the _processing_ of option forwarding
        /// is done at the ParseResult level, where we don't have the generic type parameter.
        /// </remarks>
        public Func<ParseResult, IEnumerable<string>>? ForwardingFunction => s_forwardingFunctions.GetValueOrDefault(option);
    }

    extension<TValue>(Option<TValue> option)
    {
        /// <summary>
        /// Internal-only helper function that ensures the provided forwarding function is only called
        /// if the option actually has a value.
        /// </summary>
        private Func<ParseResult, IEnumerable<string>> GetForwardingFunction(Func<TValue, IEnumerable<string>> func)
        {
            return (ParseResult parseResult) =>
            {
                if (parseResult.GetResult(option) is OptionResult r)
                {
                    if (r.GetValueOrDefault<TValue>() is TValue value)
                    {
                        return func(value);
                    }
                    else
                    {
                        return [];
                    }
                }
                return [];
            };
        }

        /// <summary>
        /// Internal-only helper function that ensures the provided forwarding function is only called
        /// if the option actually has a value.
        /// </summary>
        private Func<ParseResult, IEnumerable<string>> GetForwardingFunction(Func<TValue, ParseResult, IEnumerable<string>> func)
        {
            return (ParseResult parseResult) =>
            {
                if (parseResult.GetResult(option) is OptionResult r)
                {
                    if (r.GetValueOrDefault<TValue>() is TValue value)
                    {
                        return func(value, parseResult);
                    }
                    else
                    {
                        return [];
                    }
                }
                return [];
            };
        }

        /// <summary>
        /// Forwards the option using the provided function to convert the option's value to zero or more string values.
        /// The function will only be called if the option has a value.
        /// </summary>
        public Option<TValue> SetForwardingFunction(Func<TValue?, IEnumerable<string>> func)
        {
            lock (s_lock)
            {
                s_forwardingFunctions[option] = option.GetForwardingFunction(func);
            }
            return option;
        }

        /// <summary>
        /// Forward the option using the provided function to convert the option's value to a single string value.
        /// The function will only be called if the option has a value.
        /// </summary>
        public Option<TValue> SetForwardingFunction(Func<TValue, string> format)
        {
            lock (s_lock)
            {
                s_forwardingFunctions[option] = option.GetForwardingFunction(o => [format(o)]);
            }
            return option;
        }

        /// <summary>
        /// Forward the option using the provided function to convert the option's value to a single string value.
        /// The function will only be called if the option has a value.
        /// </summary>
        public Option<TValue> SetForwardingFunction(Func<TValue?, ParseResult, IEnumerable<string>> func)
        {
            lock (s_lock)
            {
                s_forwardingFunctions[option] = option.GetForwardingFunction(func);
            }
            return option;
        }

        /// <summary>
        /// Forward the option as multiple calculated string values from whatever the option's value is.
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public Option<TValue> ForwardAsMany(Func<TValue?, IEnumerable<string>> format) => option.SetForwardingFunction(format);

        /// <summary>
        /// Forward the option as its own name.
        /// </summary>
        /// <returns></returns>
        public Option<TValue> Forward() => option.SetForwardingFunction((TValue? o) => [option.Name]);

        /// <summary>
        /// Forward the option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded.
        /// </summary>
        public Option<TValue> ForwardAs(string value) => option.SetForwardingFunction((TValue? o) => [value]);

        /// <summary>
        /// Forward the option as a singular calculated string value.
        /// </summary>
        public Option<TValue> ForwardAsSingle(Func<TValue, string> format) => option.SetForwardingFunction(format);
    }

    extension(Option<bool> option)
    {
        /// <summary>
        /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
        /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
        /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
        /// </summary>
        public Option<bool> ForwardIfEnabled(string value) => option.SetForwardingFunction((bool o) => o ? [value] : []);
        /// <summary>
        /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
        /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
        /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
        /// </summary>
        public Option<bool> ForwardIfEnabled(string[] value) => option.SetForwardingFunction((bool o) => o ? value : []);

        /// <summary>
        /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
        /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
        /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
        /// </summary>
        public Option<bool> ForwardAs(string value) => option.ForwardIfEnabled(value);
    }

    extension(Option<IEnumerable<string>> option)
    {
        /// <summary>
        /// Foreach argument in the option's value, yield the <paramref name="alias"/> followed by the argument.
        /// </summary>
        public Option<IEnumerable<string>> ForwardAsManyArgumentsEachPrefixedByOption(string alias) =>
            option.ForwardAsMany(o => ForwardedArguments(alias, o));
    }

    extension(ParseResult parseResult)
    {
        /// <summary>
        /// Calls the forwarding functions for all options that have declared a forwarding function (via <see cref="ForwardedOptionExtensions"/>'s extension members) in the provided <see cref="ParseResult"/>.
        /// </summary>
        /// <param name="command">If not provided, uses the <see cref="ParseResult.CommandResult" />'s <see cref="CommandResult.Command"/>.</param>
        public IEnumerable<string> OptionValuesToBeForwarded(Command? command = null)
            => parseResult.OptionValuesToBeForwarded((command ?? parseResult.CommandResult.Command).Options);

        /// <summary>
        /// Calls the forwarding functions for all options that have declared a forwarding function (via <see cref="ForwardedOptionExtensions"/>'s extension members) in the provided <see cref="ParseResult"/>.
        /// </summary>
        /// <param name="command">If not provided, uses the <see cref="ParseResult.CommandResult" />'s <see cref="CommandResult.Command"/>.</param>
        public IEnumerable<string> OptionValuesToBeForwarded(IEnumerable<Option> options)
            => options
                .Select(o => o.ForwardingFunction)
                .SelectMany(f => f is not null ? f(parseResult) : []);

        /// <summary>
        /// Tries to find the first option named <paramref name="alias"/> in <paramref name="command"/>, and if found,
        /// invokes its forwarding function (if any) and returns the result. If no option with that name is found, or if the option
        /// has no forwarding function, returns an empty enumeration.
        /// </summary>
        public IEnumerable<string> ForwardedOptionValues(Command command, string alias)
        {
            var func = command.Options?
                .Where(o =>
                    (o.Name.Equals(alias) || o.Aliases.Contains(alias))
                    && o.ForwardingFunction is not null)
                .FirstOrDefault()?.ForwardingFunction;
            return func?.Invoke(parseResult) ?? [];
        }
    }

    /// <summary>
    /// For each argument in <paramref name="arguments"/>, yield the <paramref name="alias"/> followed by the argument.
    /// </summary>
    private static IEnumerable<string> ForwardedArguments(string alias, IEnumerable<string>? arguments)
    {
        foreach (string arg in arguments ?? [])
        {
            yield return alias;
            yield return arg;
        }
    }
}
