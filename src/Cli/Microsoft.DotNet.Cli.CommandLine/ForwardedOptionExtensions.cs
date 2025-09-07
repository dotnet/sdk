using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.CommandLine;

public static class ForwardedOptionExtensions
{
    extension<TValue>(ForwardedOption<TValue> option)
    {
        public ForwardedOption<TValue> SetForwardingFunction(Func<TValue?, IEnumerable<string>> func)
        {
            option.ForwardingFunction = option.GetForwardingFunction(func);
            return option;
        }

        public ForwardedOption<TValue> SetForwardingFunction(Func<TValue, string> format)
        {
            option.ForwardingFunction = option.GetForwardingFunction((o) => [format(o)]);
            return option;
        }

        public ForwardedOption<TValue> SetForwardingFunction(Func<TValue?, ParseResult, IEnumerable<string>> func)
        {
            option.ForwardingFunction = parseResult =>
            {
                if (parseResult.GetResult(option) is OptionResult argresult && argresult.GetValue(option) is TValue validValue)
                {
                    return func(validValue, parseResult) ?? [];
                }
                else
                {
                    return [];
                }
            };
            return option;
        }
        public ForwardedOption<TValue> ForwardAsMany(Func<TValue?, IEnumerable<string>> format) => option.SetForwardingFunction(format);

        public ForwardedOption<TValue> Forward() => option.SetForwardingFunction((TValue? o) => [option.Name]);

        /// <summary>
        /// Forward the option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded.
        /// </summary>
        public ForwardedOption<TValue> ForwardAs(string value) => option.SetForwardingFunction((TValue? o) => [value]);

        public ForwardedOption<TValue> ForwardAsSingle(Func<TValue, string> format) => option.SetForwardingFunction(format);
    }

    extension(ForwardedOption<bool> option)
    {
        /// <summary>
        /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
        /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
        /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
        /// </summary>
        public ForwardedOption<bool> ForwardIfEnabled(string value) => option.SetForwardingFunction((bool o) => o ? [value] : []);
        /// <summary>
        /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
        /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
        /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
        /// </summary>
        public ForwardedOption<bool> ForwardIfEnabled(string[] value) => option.SetForwardingFunction((bool o) => o ? value : []);

        /// <summary>
        /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
        /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
        /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
        /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
        /// </summary>
        public ForwardedOption<bool> ForwardAs(string value) => option.ForwardIfEnabled(value);
    }

    extension(ForwardedOption<IEnumerable<string>> option)
    {
        public ForwardedOption<IEnumerable<string>> ForwardAsManyArgumentsEachPrefixedByOption(string alias) =>
            option.ForwardAsMany(o => ForwardedArguments(alias, o));
    }

    extension(ParseResult parseResult)
    {
        /// <summary>
        /// Calls the forwarding functions for all options that implement <see cref="IForwardedOption"/> in the provided <see cref="ParseResult"/>.
        /// </summary>
        /// <param name="parseResult"></param>
        /// <param name="command">If not provided, uses the <see cref="ParseResult.CommandResult" />'s <see cref="CommandResult.Command"/>.</param>
        /// <returns></returns>
        public IEnumerable<string> OptionValuesToBeForwarded(Command? command = null) =>
            (command ?? parseResult.CommandResult.Command).Options
                .OfType<IForwardedOption>()
                .Select(o => o.GetForwardingFunction())
                .SelectMany(f => f is not null ? f(parseResult) : []);

        public IEnumerable<string> ForwardedOptionValues(Command command, string alias)
        {
            var func = command.Options?
                .Where(o => o.Name.Equals(alias) || o.Aliases.Contains(alias))?
                .OfType<IForwardedOption>()?
                .FirstOrDefault()?
                .GetForwardingFunction();
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
