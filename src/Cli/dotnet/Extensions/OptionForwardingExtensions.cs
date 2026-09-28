// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.Commands.Test;

namespace Microsoft.DotNet.Cli.Extensions;

public static class OptionForwardingExtensions
{
    public static ForwardedOption<T> Forward<T>(this ForwardedOption<T> option) => option.SetForwardingFunction((T? o) => [option.Name]);

    /// <summary>
    /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
    /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
    /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
    /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
    /// </summary>
    public static ForwardedOption<bool> ForwardAs(this ForwardedOption<bool> option, string value) => option.ForwardIfEnabled(value);

    /// <summary>
    /// Forward the option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
    /// any implicit value calculation will cause the string value to be forwarded.
    /// </summary>
    public static ForwardedOption<T> ForwardAs<T>(this ForwardedOption<T> option, string value) => option.SetForwardingFunction((T? o) => [value]);

    public static ForwardedOption<T> ForwardAsSingle<T>(this ForwardedOption<T> option, Func<T, string> format) => option.SetForwardingFunction(format);

    /// <summary>
    /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
    /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
    /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
    /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
    /// </summary>
    public static ForwardedOption<bool> ForwardIfEnabled(this ForwardedOption<bool> option, string value) => option.SetForwardingFunction((bool o) => o ? [value] : []);
    /// <summary>
    /// Forward the boolean option as a string value. This value will be forwarded as long as the option has a OptionResult - which means that
    /// any implicit value calculation will cause the string value to be forwarded. For boolean options specifically, if the option is zero arity
    /// and has no default value factory, S.CL will synthesize a true or false value based on whether the option was provided or not, so we need to
    /// add an additional implicit 'value is true' check to prevent accidentally forwarding the option for flags that are absent..
    /// </summary>
    public static ForwardedOption<bool> ForwardIfEnabled(this ForwardedOption<bool> option, string[] value) => option.SetForwardingFunction((bool o) => o ? value : []);

    /// <summary>
    /// Set up an option to be forwarded as an output path to MSBuild
    /// </summary>
    /// <param name="option">The command line option</param>
    /// <param name="outputPropertyName">The property name for the output path (such as OutputPath or PublishDir)</param>
    /// <param name="surroundWithDoubleQuotes">Whether the path should be surrounded with double quotes.  This may not be necessary but preserves the previous behavior of "dotnet test"</param>
    /// <returns>The option</returns>
    public static ForwardedOption<string> ForwardAsOutputPath(this ForwardedOption<string> option, string outputPropertyName, bool surroundWithDoubleQuotes = false)
    {
        return option.SetForwardingFunction((string? o) =>
        {
            if (o is null)
            {
                return [];
            }
            string argVal = CommandDirectoryContext.GetFullPath(o);
            if (surroundWithDoubleQuotes)
            {
                //  Not sure if this is necessary, but this is what "dotnet test" previously did and so we are
                //  preserving the behavior here after refactoring
                argVal = TestCommandParser.SurroundWithDoubleQuotes(argVal);
            }
            return [
                $"--property:{outputPropertyName}={argVal}",
                "--property:_CommandLineDefinedOutputPath=true"
            ];
        });
    }

    /// <summary>
    /// Set up an option to be forwarded as an MSBuild property
    /// This will parse the values as MSBuild properties and forward them in the format <c>optionName:key=value</c>.
    /// For example, if the option is named "--property", and the values are "A=B" and "C=D", it will be forwarded as:
    /// <c>--property:A=B --property:C=D</c>.
    /// This is useful for options that can take multiple key-value pairs, such as --property.
    /// </summary>
    public static ForwardedOption<ReadOnlyDictionary<string, string>?> ForwardAsMSBuildProperty(this ForwardedOption<ReadOnlyDictionary<string, string>?> option) => option
        .SetForwardingFunction(propertyDict => ForwardedMSBuildPropertyValues(propertyDict, option.Name));

    private static IEnumerable<string> ForwardedMSBuildPropertyValues(ReadOnlyDictionary<string, string>? properties, string optionName)
    {
        if (properties is null || properties.Count == 0)
        {
            return Enumerable.Empty<string>();
        }

        return properties.Select(kv => $"{optionName}:{kv.Key}={kv.Value}");
    }

    public static Option<T> ForwardAsMany<T>(this ForwardedOption<T> option, Func<T?, IEnumerable<string>> format) => option.SetForwardingFunction(format);

    public static Option<IEnumerable<string>> ForwardAsManyArgumentsEachPrefixedByOption(this ForwardedOption<IEnumerable<string>> option, string alias) => option.ForwardAsMany(o => ForwardedArguments(alias, o));

    /// <summary>
    /// Calls the forwarding functions for all options that implement <see cref="IForwardedOption"/> in the provided <see cref="ParseResult"/>.
    /// </summary>
    /// <param name="parseResult"></param>
    /// <param name="command">If not provided, uses the <see cref="ParseResult.CommandResult" />'s <see cref="CommandResult.Command"/>.</param>
    /// <returns></returns>
    public static IEnumerable<string> OptionValuesToBeForwarded(this ParseResult parseResult, Command? command = null) =>
        (command ?? parseResult.CommandResult.Command).Options
            .OfType<IForwardedOption>()
            .Select(o => o.GetForwardingFunction())
            .SelectMany(f => f is not null ? f(parseResult) : []);

    public static IEnumerable<string> ForwardedOptionValues<T>(this ParseResult parseResult, Command command, string alias)
    {
        var func = command.Options?
            .Where(o => o.Name.Equals(alias) || o.Aliases.Contains(alias))?
            .OfType<IForwardedOption>()?
            .FirstOrDefault()?
            .GetForwardingFunction();
        return func?.Invoke(parseResult) ?? [];
    }

    /// <summary>
    /// Forces an option that represents a collection-type to only allow a single
    /// argument per instance of the option. This means that you'd have to
    /// use the option multiple times to pass multiple values.
    /// This prevents ambiguity in parsing when argument tokens may appear after the option.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="option"></param>
    /// <returns></returns>
    public static T AllowSingleArgPerToken<T>(this T option) where T : Option
    {
        option.AllowMultipleArgumentsPerToken = false;
        return option;
    }


    public static T AggregateRepeatedTokens<T>(this T option) where T : Option
    {
        option.AllowMultipleArgumentsPerToken = true;
        return option;
    }

    public static Option<T> Hide<T>(this Option<T> option)
    {
        option.Hidden = true;
        return option;
    }

    private static IEnumerable<string> ForwardedArguments(string alias, IEnumerable<string>? arguments)
    {
        foreach (string arg in arguments ?? [])
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

    public ForwardedOption(string name, params string[] aliases) : base(name, aliases)
    {
        ForwardingFunction = _ => [];
    }

    public ForwardedOption(string name, Func<ArgumentResult, T> parseArgument, string? description = null)
        : base(name)
    {
        CustomParser = parseArgument;
        Description = description;
        ForwardingFunction = _ => [];
    }

    public ForwardedOption<T> SetForwardingFunction(Func<T?, IEnumerable<string>> func)
    {
        ForwardingFunction = GetForwardingFunction(func);
        return this;
    }

    public ForwardedOption<T> SetForwardingFunction(Func<T, string> format)
    {
        ForwardingFunction = GetForwardingFunction((o) => [format(o)]);
        return this;
    }

    public ForwardedOption<T> SetForwardingFunction(Func<T?, ParseResult, IEnumerable<string>> func)
    {
        ForwardingFunction = (ParseResult parseResult) =>
        {
            if (parseResult.GetResult(this) is OptionResult argresult && argresult.GetValue(this) is T validValue)
            {
                return func(validValue, parseResult) ?? [];
            }
            else
            {
                return [];
            }
        };
        return this;
    }

    public Func<ParseResult, IEnumerable<string>> GetForwardingFunction(Func<T, IEnumerable<string>> func)
    {
        return (ParseResult parseResult) =>
        {
            if (parseResult.GetResult(this) is OptionResult r)
            {
                if (r.GetValueOrDefault<T>() is T value)
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

    public Func<ParseResult, IEnumerable<string>> GetForwardingFunction()
    {
        return ForwardingFunction;
    }
}

public class DynamicForwardedOption<T> : ForwardedOption<T>, IDynamicOption
{
    public DynamicForwardedOption(string name, Func<ArgumentResult, T> parseArgument, string? description = null)
        : base(name, parseArgument, description)
    {
    }

    public DynamicForwardedOption(string name, params string[] aliases) : base(name, aliases) { }
}
