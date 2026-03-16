// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// Extension methods for safely navigating ParseResult and SymbolResult to get option values.
/// </summary>
public static class ResultNavigationExtensions
{
    /// <summary>
    /// Only returns the value for this option if the option is present and there are no parse errors for that option.
    /// This allows cross-cutting code like the telemetry filters to safely get the value without throwing on null-ref errors.
    /// If you are inside a command handler or 'normal' System.CommandLine code then you don't need this - the parse error handling
    /// will have covered these cases.
    /// </summary>
    public static T? SafelyGetValueForOption<T>(this ParseResult parseResult, Option<T> optionToGet)
    {
        if (parseResult.GetResult(optionToGet) is OptionResult optionResult // only return a value if there _is_ a value - default or otherwise
            && !parseResult.Errors.Any(e => e.SymbolResult == optionResult) // only return a value if this isn't a parsing error
            && optionResult.Option.ValueType.IsAssignableTo(typeof(T))) // only return a value if coercing the type won't error
        {
            // shouldn't happen because of the above checks, but we can be safe since this is only used in telemetry, and should
            // be resistant to errors
            try
            {
                return optionResult.GetValue(optionToGet);
            }
            catch
            {
                return default;
            }
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// Only returns the value for this option if the option is present and there are no parse errors for that option.
    /// This allows cross-cutting code like the telemetry filters to safely get the value without throwing on null-ref errors.
    /// If you are inside a command handler or 'normal' System.CommandLine code then you don't need this - the parse error handling
    /// will have covered these cases.
    /// </summary>
    public static T? SafelyGetValueForOption<T>(this ParseResult parseResult, string name)
    {
        if (parseResult.GetResult(name) is OptionResult optionResult // only return a value if there _is_ a value - default or otherwise
            && !parseResult.Errors.Any(e => e.SymbolResult == optionResult) // only return a value if this isn't a parsing error
            && optionResult.Option.ValueType.IsAssignableTo(typeof(T))) // only return a value if coercing the type won't error
        {
            // shouldn't happen because of the above checks, but we can be safe since this is only used in telemetry, and should
            // be resistant to errors
            try
            {
                return optionResult.GetValue<T>(name);
            }
            catch
            {
                return default;
            }
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// Checks if the option is present and not implicit (i.e. not set by default).
    /// This is useful for checking if the user has explicitly set an option, as opposed to it being set by default.
    /// </summary>
    public static bool HasOption(this ParseResult parseResult, Option option) => parseResult.GetResult(option) is OptionResult or && !or.Implicit;

    /// <summary>
    /// Checks if the option with given name is present and not implicit (i.e. not set by default).
    /// This is useful for checking if the user has explicitly set an option, as opposed to it being set by default.
    /// </summary>
    public static bool HasOption(this ParseResult parseResult, string name)
        => parseResult.GetResult(name) is OptionResult or && !or.Implicit;

    /// <summary>
    /// Checks if the option is present and not implicit (i.e. not set by default).
    /// This is useful for checking if the user has explicitly set an option, as opposed to it being set by default.
    /// </summary>
    public static bool HasOption(this SymbolResult symbolResult, Option option) => symbolResult.GetResult(option) is OptionResult or && !or.Implicit;

    /// <summary>
    /// Checks if the option with given name is present and not implicit (i.e. not set by default).
    /// This is useful for checking if the user has explicitly set an option, as opposed to it being set by default.
    /// </summary>
    public static bool HasOption(this SymbolResult symbolResult, string name)
        => symbolResult.GetResult(name) is OptionResult or && !or.Implicit;
}
